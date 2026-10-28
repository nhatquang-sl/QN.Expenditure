package orderfill

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

// Command is empty — this handler operates on all OPEN paper orders.
type Command struct{}

// Result is empty — filling is a side effect.
type Result struct{}

const fillTimeframe = "15min"
const fillCandleLimit = 200

type handler struct {
	queries   *generated.Queries
	kucoinSvc *kucoin.Service
	logger    *slog.Logger
}

// NewHandler returns a Handler that checks OPEN paper orders against 15-minute
// market data and fills any whose price has been crossed.
func NewHandler(queries *generated.Queries, kucoinSvc *kucoin.Service, logger *slog.Logger) app.Handler[Command, Result] {
	return &handler{queries: queries, kucoinSvc: kucoinSvc, logger: logger}
}

func (h *handler) Handle(ctx context.Context, _ Command) (Result, error) {
	orders, err := h.queries.GetOpenPaperOrders(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("order/fill: querying open orders: %w", err)
	}

	for _, order := range orders {
		if err := h.tryFill(ctx, order); err != nil {
			h.logger.ErrorContext(ctx, "order/fill: error processing order",
				slog.String("order_id", order.Id),
				slog.String("symbol", order.Symbol),
				slog.Any("error", err),
			)
		}
	}

	return Result{}, nil
}

func (h *handler) tryFill(ctx context.Context, order generated.GetOpenPaperOrdersRow) error {
	candles, err := h.kucoinSvc.GetClosedCandles(ctx, order.Symbol, fillTimeframe, fillCandleLimit)
	if err != nil {
		return fmt.Errorf("fetching candles for %s: %w", order.Symbol, err)
	}

	// Only consider candles opened at or after the order creation time.
	filled := false
	for _, c := range candles {
		if c.OpenTime.Before(order.CreatedAt) {
			continue
		}
		if isFilled(order.Side, order.FillPrice, c.Low, c.High) {
			filled = true
			break
		}
	}

	if !filled {
		return nil
	}

	now := time.Now().UTC()

	if err := h.queries.UpdatePaperOrderStatus(ctx, generated.UpdatePaperOrderStatusParams{
		Id:        order.Id,
		Status:    "FILLED",
		UpdatedAt: now,
	}); err != nil {
		return fmt.Errorf("updating order status to FILLED: %w", err)
	}

	fillId := newUUID()
	if err := h.queries.InsertPaperFill(ctx, generated.InsertPaperFillParams{
		Id:           fillId,
		CandidateId:  order.CandidateId,
		PaperOrderId: order.Id,
		Quantity:     order.Quantity,
		Price:        order.FillPrice,
		ExecutedAt:   now,
	}); err != nil {
		return fmt.Errorf("inserting paper fill: %w", err)
	}

	if err := h.queries.UpsertPosition(ctx, generated.UpsertPositionParams{
		Id:            newUUID(),
		CandidateId:   order.CandidateId,
		Symbol:        order.Symbol,
		Asset:         baseAsset(order.Symbol),
		Quantity:      order.Quantity,
		AvgEntryPrice: order.FillPrice,
		CurrentPrice:  order.FillPrice,
		UnrealizedPnl: 0,
		CreatedAt:     now,
		UpdatedAt:     now,
	}); err != nil {
		return fmt.Errorf("upserting position: %w", err)
	}

	h.logger.InfoContext(ctx, "paper_order_filled",
		slog.String("candidate_id", order.CandidateId),
		slog.String("order_id", order.Id),
		slog.String("symbol", order.Symbol),
		slog.String("side", order.Side),
		slog.Float64("fill_price", order.FillPrice),
		slog.Float64("quantity", order.Quantity),
	)

	return nil
}

// isFilled returns true when the candle's price range crosses the fill price.
// BUY: filled when Low ≤ fillPrice (price dipped to or below entry).
// SELL: filled when High ≥ fillPrice (price rose to or above entry).
func isFilled(side string, fillPrice, low, high float64) bool {
	switch strings.ToUpper(side) {
	case "BUY":
		return low <= fillPrice
	case "SELL":
		return high >= fillPrice
	default:
		return false
	}
}

// baseAsset strips the known quote currency suffix from a symbol.
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
