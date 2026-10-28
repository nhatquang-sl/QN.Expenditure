package riskevaluate

import (
	"context"
	"crypto/rand"
	"encoding/json"
	"fmt"
	"log/slog"
	"time"

	riskshared "trading_bot/internal/application/risk/shared"
	"trading_bot/internal/database/generated"

	app "qn.expenditure/shared/app"
)

// Command carries the ID of a persisted CandidateTrade to risk-evaluate.
type Command struct {
	TradeId string
}

// Result reports the risk decision that was made.
type Result struct {
	DecisionId string
	Decision   string
}

// snapshotInput holds the portfolio state derived from account balances and positions.
type snapshotInput struct {
	CandidateId   string
	Equity        float64
	AvailableCash float64
	TotalExposure float64
	UnrealizedPnl float64
	OpenPositions int
}

type handler struct {
	queries *generated.Queries
	gate    *HardRiskGate
	limits  RiskLimits
	logger  *slog.Logger
}

// NewHandler wires dependencies and returns a Handler that runs: portfolio
// snapshot → risk context assembly → hard gate → risk decision persistence.
func NewHandler(
	queries *generated.Queries,
	gate *HardRiskGate,
	limits RiskLimits,
	logger *slog.Logger,
) app.Handler[Command, Result] {
	return &handler{
		queries: queries,
		gate:    gate,
		limits:  limits,
		logger:  logger,
	}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	// 1. Fetch the candidate trade from DB.
	h.logger.InfoContext(ctx, "risk_evaluation_started", slog.String("candidate_id", cmd.TradeId))
	ct, err := h.queries.GetCandidateTrade(ctx, cmd.TradeId)
	if err != nil {
		return Result{}, fmt.Errorf("risk/evaluate: fetching candidate trade %s: %w", cmd.TradeId, err)
	}

	candidate := CandidateInfo{
		Id:                ct.Id,
		Symbol:            ct.Symbol,
		Side:              ct.Side,
		EntryPrice:        ct.EntryPrice,
		EstimatedExposure: h.limits.MinOrderSizeUsdt,
	}

	// 2. Read portfolio state directly from DB and compute snapshot.
	balances, err := h.queries.GetAccountBalances(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("risk/evaluate: fetching account balances: %w", err)
	}
	positions, err := h.queries.GetOpenPositions(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("risk/evaluate: fetching open positions: %w", err)
	}
	snap := computeSnapshotInput(balances, positions)
	snap.CandidateId = ct.Id

	// 3. Assemble and persist RiskContext + portfolio snapshot.
	riskCtx, riskContextId, err := h.buildRiskContext(ctx, candidate, snap)
	if err != nil {
		return Result{}, fmt.Errorf("risk/evaluate: building risk context: %w", err)
	}
	h.logger.InfoContext(ctx, "risk_context_created",
		slog.String("candidate_id", ct.Id),
		slog.String("symbol", ct.Symbol),
		slog.String("risk_context_id", riskContextId),
	)

	// 4. Evaluate hard risk gate (pure function — no I/O).
	decision := h.gate.Evaluate(riskCtx)

	// 5. Persist risk decision (always — both APPROVE and REJECT paths).
	decisionId := newUUID()
	if err := h.queries.InsertRiskDecision(ctx, generated.InsertRiskDecisionParams{
		Id:            decisionId,
		CandidateId:   ct.Id,
		RiskContextId: riskContextId,
		Decision:      decision.Decision,
		PositionSize:  decision.PositionSize,
		Reasons:       decision.Reasons,
		CreatedAt:     time.Now().UTC(),
	}); err != nil {
		return Result{}, fmt.Errorf("risk/evaluate: saving risk decision: %w", err)
	}
	h.logger.InfoContext(ctx, "risk_decision_created",
		slog.String("candidate_id", ct.Id),
		slog.String("decision", decision.Decision),
		slog.Any("reasons", decision.Reasons),
	)

	if decision.Decision != riskshared.DecisionApprove {
		h.logger.InfoContext(ctx, "risk_decision_rejected",
			slog.String("candidate_id", ct.Id),
			slog.String("symbol", ct.Symbol),
			slog.Any("reasons", decision.Reasons),
		)
	}

	return Result{DecisionId: decisionId, Decision: decision.Decision}, nil
}

// buildRiskContext assembles a RiskContext, persists the portfolio snapshot and
// risk context records, and returns the assembled context and its DB ID.
func (h *handler) buildRiskContext(ctx context.Context, candidate CandidateInfo, snap snapshotInput) (RiskContext, string, error) {
	snapshotId, err := h.persistPortfolioSnapshot(ctx, snap)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: persisting portfolio snapshot: %w", err)
	}

	symbolExposure, err := h.buildSymbolExposure(ctx)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: reading open positions: %w", err)
	}

	history, err := h.buildTradingHistory(ctx)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: reading trade history: %w", err)
	}

	riskCtx := RiskContext{
		Candidate: candidate,
		Portfolio: PortfolioInfo{
			TotalExposure:  snap.TotalExposure,
			AvailableCash:  snap.AvailableCash,
			OpenPositions:  snap.OpenPositions,
			SymbolExposure: symbolExposure,
		},
		TradingHistory: history,
		Limits:         h.limits,
	}

	rawJSON, err := json.Marshal(riskCtx)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: marshaling risk context: %w", err)
	}

	riskContextId := newUUID()
	if err := h.queries.InsertRiskContext(ctx, generated.InsertRiskContextParams{
		Id:                  riskContextId,
		CandidateId:         candidate.Id,
		PortfolioSnapshotId: snapshotId,
		GeneratedAt:         time.Now().UTC(),
		RawJson:             string(rawJSON),
	}); err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: saving risk context: %w", err)
	}

	return riskCtx, riskContextId, nil
}

func (h *handler) persistPortfolioSnapshot(ctx context.Context, snap snapshotInput) (string, error) {
	id := newUUID()
	err := h.queries.InsertPortfolioSnapshot(ctx, generated.InsertPortfolioSnapshotParams{
		Id:            id,
		CandidateId:   snap.CandidateId,
		Equity:        snap.Equity,
		AvailableCash: snap.AvailableCash,
		TotalExposure: snap.TotalExposure,
		UnrealizedPnl: snap.UnrealizedPnl,
		OpenPositions: int32(snap.OpenPositions),
		CapturedAt:    time.Now().UTC(),
	})
	if err != nil {
		return "", err
	}
	return id, nil
}

func (h *handler) buildSymbolExposure(ctx context.Context) (map[string]float64, error) {
	positions, err := h.queries.GetOpenPositions(ctx)
	if err != nil {
		return nil, err
	}
	m := make(map[string]float64, len(positions))
	for _, p := range positions {
		m[p.Symbol] = p.CurrentPrice * p.Quantity
	}
	return m, nil
}

func (h *handler) buildTradingHistory(ctx context.Context) (TradingHistoryInfo, error) {
	today := time.Now().UTC().Truncate(24 * time.Hour)
	dailyPnL, err := h.queries.GetDailyPnL(ctx, today)
	if err != nil {
		return TradingHistoryInfo{}, err
	}

	recent, err := h.queries.GetRecentTradeOutcomes(ctx, 20)
	if err != nil {
		return TradingHistoryInfo{}, err
	}

	return TradingHistoryInfo{
		DailyPnL:          dailyPnL,
		ConsecutiveLosses: countConsecutiveLosses(recent),
		RecentWinRate:     calcWinRate(recent),
	}, nil
}

// computeSnapshotInput derives portfolio state from raw DB rows. Pure function — no I/O.
// CandidateId is left empty; the caller sets it before passing to buildRiskContext.
func computeSnapshotInput(balances []generated.AccountBalance, positions []generated.Position) snapshotInput {
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

	return snapshotInput{
		Equity:        availableCash + totalExposure,
		AvailableCash: availableCash,
		TotalExposure: totalExposure,
		UnrealizedPnl: unrealizedPnl,
		OpenPositions: len(positions),
	}
}

// countConsecutiveLosses counts trailing losses from the newest outcome backward.
// outcomes must be ordered newest-first (as returned by GetRecentTradeOutcomes).
func countConsecutiveLosses(outcomes []generated.TradeOutcome) int {
	count := 0
	for _, o := range outcomes {
		if o.NetPnl < 0 {
			count++
		} else {
			break
		}
	}
	return count
}

// calcWinRate returns the fraction of outcomes with non-negative NetPnl.
func calcWinRate(outcomes []generated.TradeOutcome) float64 {
	if len(outcomes) == 0 {
		return 0
	}
	wins := 0
	for _, o := range outcomes {
		if o.NetPnl >= 0 {
			wins++
		}
	}
	return float64(wins) / float64(len(outcomes))
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
