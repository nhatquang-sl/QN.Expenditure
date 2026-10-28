package orderclose

import (
	"context"
	"crypto/rand"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"trading_bot/internal/database/generated"
	"trading_bot/internal/services/kucoin"

	app "qn.expenditure/shared/app"
)

// Command carries the ID of a FILLED paper order to close.
type Command struct {
	PaperOrderId string
}

// Result reports the net PnL of the closed trade.
type Result struct {
	NetPnL float64
}

const closeCandleTimeframe = "15min"

type handler struct {
	queries   *generated.Queries
	kucoinSvc *kucoin.Service
	logger    *slog.Logger
}

// NewHandler returns a Handler that closes a FILLED paper order: fetches current
// exit price, writes a TradeOutcome, and zeroes the position.
func NewHandler(queries *generated.Queries, kucoinSvc *kucoin.Service, logger *slog.Logger) app.Handler[Command, Result] {
	return &handler{queries: queries, kucoinSvc: kucoinSvc, logger: logger}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	// 1. Fetch the FILLED paper order.
	order, err := h.queries.GetFilledPaperOrder(ctx, cmd.PaperOrderId)
	if err != nil {
		return Result{}, fmt.Errorf("order/close: fetching paper order %s: %w", cmd.PaperOrderId, err)
	}

	// 2. Fetch the fill record for entry price and quantity.
	fill, err := h.queries.GetPaperFillByOrderId(ctx, order.Id)
	if err != nil {
		return Result{}, fmt.Errorf("order/close: fetching paper fill for order %s: %w", order.Id, err)
	}

	// 3. Fetch current exit price from KuCoin (latest 15min candle close).
	candles, err := h.kucoinSvc.GetClosedCandles(ctx, order.Symbol, closeCandleTimeframe, 1)
	if err != nil {
		return Result{}, fmt.Errorf("order/close: fetching current price for %s: %w", order.Symbol, err)
	}
	if len(candles) == 0 {
		return Result{}, fmt.Errorf("order/close: no candles returned for %s", order.Symbol)
	}
	exitPrice := candles[len(candles)-1].Close

	// 4. Compute PnL.
	entryPrice := fill.Price
	quantity := fill.Quantity
	grossPnL := computeGrossPnL(order.Side, entryPrice, exitPrice, quantity)
	netPnL := grossPnL // fees = 0 in Week 3–4

	// 5. Write TradeOutcome (immutable insert).
	now := time.Now().UTC()
	if err := h.queries.InsertTradeOutcome(ctx, generated.InsertTradeOutcomeParams{
		Id:          newUUID(),
		CandidateId: order.CandidateId,
		PaperFillId: fill.Id,
		EntryPrice:  entryPrice,
		ExitPrice:   exitPrice,
		Quantity:    quantity,
		GrossPnl:    grossPnL,
		NetPnl:      netPnL,
		ClosedAt:    now,
	}); err != nil {
		return Result{}, fmt.Errorf("order/close: inserting trade outcome: %w", err)
	}

	// 6. Close the position (set quantity → 0).
	if err := h.queries.ClosePosition(ctx, generated.ClosePositionParams{
		Symbol:    order.Symbol,
		UpdatedAt: now,
	}); err != nil {
		return Result{}, fmt.Errorf("order/close: closing position for %s: %w", order.Symbol, err)
	}

	h.logger.InfoContext(ctx, "trade_outcome_created",
		slog.String("candidate_id", order.CandidateId),
		slog.String("order_id", order.Id),
		slog.String("symbol", order.Symbol),
		slog.String("side", order.Side),
		slog.Float64("entry_price", entryPrice),
		slog.Float64("exit_price", exitPrice),
		slog.Float64("net_pnl", netPnL),
	)

	return Result{NetPnL: netPnL}, nil
}

// computeGrossPnL calculates directional PnL.
// BUY: profit when ExitPrice > EntryPrice.
// SELL: profit when ExitPrice < EntryPrice.
func computeGrossPnL(side string, entryPrice, exitPrice, quantity float64) float64 {
	switch strings.ToUpper(side) {
	case "SELL":
		return (entryPrice - exitPrice) * quantity
	default: // BUY
		return (exitPrice - entryPrice) * quantity
	}
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
