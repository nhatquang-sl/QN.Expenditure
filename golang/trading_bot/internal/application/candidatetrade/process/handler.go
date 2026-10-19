package processcandidatetrade

import (
	"context"
	"crypto/rand"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"trading_bot/internal/database/generated"
	"trading_bot/internal/risk"

	app "qn.expenditure/shared/app"
)

// Command carries the ID of a persisted CandidateTrade to evaluate and, if
// approved, execute as a paper trade.
type Command struct {
	TradeId string
}

// Result reports the risk decision that was made.
type Result struct {
	Decision string
	Approved bool
}

type handler struct {
	queries *generated.Queries
	riskSvc *risk.RiskContextService
	gate    *risk.HardRiskGate
	limits  risk.RiskLimits
	logger  *slog.Logger
}

// NewHandler wires all dependencies and returns a Handler that runs the full
// candidate pipeline: portfolio snapshot → risk context → hard gate → paper execution.
func NewHandler(
	queries *generated.Queries,
	riskSvc *risk.RiskContextService,
	gate *risk.HardRiskGate,
	limits risk.RiskLimits,
	logger *slog.Logger,
) app.Handler[Command, Result] {
	return &handler{
		queries: queries,
		riskSvc: riskSvc,
		gate:    gate,
		limits:  limits,
		logger:  logger,
	}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	// 1. Fetch the candidate trade from DB.
	ct, err := h.queries.GetCandidateTrade(ctx, cmd.TradeId)
	if err != nil {
		return Result{}, fmt.Errorf("process: fetching candidate trade %s: %w", cmd.TradeId, err)
	}

	candidate := risk.CandidateInfo{
		Id:                ct.Id,
		Symbol:            ct.Symbol,
		Side:              ct.Side,
		EntryPrice:        ct.EntryPrice,
		EstimatedExposure: h.limits.MinOrderSizeUsdt,
	}

	// 2. Read portfolio state directly from DB and compute snapshot.
	balances, err := h.queries.GetAccountBalances(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("process: fetching account balances: %w", err)
	}
	positions, err := h.queries.GetOpenPositions(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("process: fetching open positions: %w", err)
	}
	snap := computeSnapshotInput(balances, positions)
	snap.CandidateId = ct.Id

	// 3. Assemble and persist RiskContext + portfolio snapshot.
	riskCtx, riskContextId, err := h.riskSvc.Build(ctx, candidate, snap)
	if err != nil {
		return Result{}, fmt.Errorf("process: building risk context: %w", err)
	}
	h.logger.InfoContext(ctx, "risk_context_created",
		slog.String("candidate_id", ct.Id),
		slog.String("symbol", ct.Symbol),
		slog.String("risk_context_id", riskContextId),
	)

	// 4. Evaluate hard risk gate (pure function — no I/O).
	decision := h.gate.Evaluate(riskCtx)

	// 5. Persist risk decision (always — both APPROVE and REJECT paths).
	riskDecisionId := newUUID()
	if err := h.queries.InsertRiskDecision(ctx, generated.InsertRiskDecisionParams{
		Id:            riskDecisionId,
		CandidateId:   ct.Id,
		RiskContextId: riskContextId,
		Decision:      decision.Decision,
		PositionSize:  decision.PositionSize,
		Reasons:       decision.Reasons,
		CreatedAt:     time.Now().UTC(),
	}); err != nil {
		return Result{}, fmt.Errorf("process: saving risk decision: %w", err)
	}
	h.logger.InfoContext(ctx, "risk_decision_created",
		slog.String("candidate_id", ct.Id),
		slog.String("decision", decision.Decision),
		slog.Any("reasons", decision.Reasons),
	)

	// 6. If rejected, log and stop.
	if decision.Decision != risk.DecisionApprove {
		h.logger.InfoContext(ctx, "risk_decision_rejected",
			slog.String("candidate_id", ct.Id),
			slog.String("symbol", ct.Symbol),
			slog.Any("reasons", decision.Reasons),
		)
		return Result{Decision: decision.Decision, Approved: false}, nil
	}

	// 7. Execute paper trade: OPEN → FILLED order, fill, position upsert.
	if err := h.executePaperTrade(ctx, ct.Id, riskDecisionId, candidate, decision.PositionSize); err != nil {
		return Result{}, fmt.Errorf("process: executing paper trade: %w", err)
	}

	return Result{Decision: decision.Decision, Approved: true}, nil
}

func (h *handler) executePaperTrade(
	ctx context.Context,
	candidateId, riskDecisionId string,
	candidate risk.CandidateInfo,
	positionSize float64,
) error {
	quantity := positionSize / candidate.EntryPrice
	now := time.Now().UTC()
	orderId := newUUID()

	if err := h.queries.InsertPaperOrder(ctx, generated.InsertPaperOrderParams{
		Id:             orderId,
		CandidateId:    candidateId,
		RiskDecisionId: riskDecisionId,
		Symbol:         candidate.Symbol,
		Side:           candidate.Side,
		Quantity:       quantity,
		FillPrice:      candidate.EntryPrice,
		Status:         "OPEN",
		CreatedAt:      now,
		UpdatedAt:      now,
	}); err != nil {
		return fmt.Errorf("saving paper order: %w", err)
	}

	if err := h.queries.UpdatePaperOrderStatus(ctx, generated.UpdatePaperOrderStatusParams{
		Id:        orderId,
		Status:    "FILLED",
		UpdatedAt: now,
	}); err != nil {
		return fmt.Errorf("updating paper order to FILLED: %w", err)
	}

	fillId := newUUID()
	if err := h.queries.InsertPaperFill(ctx, generated.InsertPaperFillParams{
		Id:           fillId,
		CandidateId:  candidateId,
		PaperOrderId: orderId,
		Quantity:     quantity,
		Price:        candidate.EntryPrice,
		ExecutedAt:   now,
	}); err != nil {
		return fmt.Errorf("saving paper fill: %w", err)
	}

	if err := h.queries.UpsertPosition(ctx, generated.UpsertPositionParams{
		Id:            newUUID(),
		CandidateId:   candidateId,
		Symbol:        candidate.Symbol,
		Asset:         baseAsset(candidate.Symbol),
		Quantity:      quantity,
		AvgEntryPrice: candidate.EntryPrice,
		CurrentPrice:  candidate.EntryPrice,
		UnrealizedPnl: 0,
		CreatedAt:     now,
		UpdatedAt:     now,
	}); err != nil {
		return fmt.Errorf("upserting position: %w", err)
	}

	h.logger.InfoContext(ctx, "paper_order_filled",
		slog.String("candidate_id", candidateId),
		slog.String("symbol", candidate.Symbol),
		slog.String("side", candidate.Side),
		slog.Float64("fill_price", candidate.EntryPrice),
		slog.Float64("quantity", quantity),
	)
	return nil
}

// computeSnapshotInput derives portfolio state from raw DB rows. Pure function — no I/O.
// CandidateId is left empty; the caller sets it before passing to RiskContextService.Build.
func computeSnapshotInput(balances []generated.AccountBalance, positions []generated.Position) risk.SnapshotInput {
	var availableCash float64
	for _, b := range balances {
		if b.Asset == "USDT" {
			availableCash = b.Available
			break
		}
	}

	var totalExposure, unrealizedPnl float64
	for _, p := range positions {
		totalExposure += p.CurrentPrice * p.Quantity
		unrealizedPnl += p.UnrealizedPnl
	}

	return risk.SnapshotInput{
		Equity:        availableCash + totalExposure,
		AvailableCash: availableCash,
		TotalExposure: totalExposure,
		UnrealizedPnl: unrealizedPnl,
		OpenPositions: len(positions),
	}
}

// baseAsset strips the known quote currency suffix from a symbol.
// e.g. "BTCUSDT" → "BTC", "ETHUSDT" → "ETH".
var knownQuotes = []string{"USDT", "USDC", "BTC", "ETH", "BNB"}

func baseAsset(symbol string) string {
	for _, q := range knownQuotes {
		if strings.HasSuffix(symbol, q) {
			return symbol[:len(symbol)-len(q)]
		}
	}
	return symbol
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
