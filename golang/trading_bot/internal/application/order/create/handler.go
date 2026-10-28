package ordercreate

import (
	"context"
	"crypto/rand"
	"fmt"
	"log/slog"
	"time"

	"trading_bot/internal/database/generated"

	app "qn.expenditure/shared/app"
)

// Command is empty — this handler operates on all approved decisions without orders.
type Command struct{}

// Result is empty — creation is a side effect.
type Result struct{}

type handler struct {
	queries *generated.Queries
	logger  *slog.Logger
}

// NewHandler returns a Handler that creates OPEN paper orders for every approved
// risk decision that does not yet have a corresponding paper order.
func NewHandler(queries *generated.Queries, logger *slog.Logger) app.Handler[Command, Result] {
	return &handler{queries: queries, logger: logger}
}

func (h *handler) Handle(ctx context.Context, _ Command) (Result, error) {
	rows, err := h.queries.GetApprovedDecisionsWithoutOrder(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("order/create: querying approved decisions: %w", err)
	}

	now := time.Now().UTC()
	for _, row := range rows {
		quantity := row.PositionSize / row.EntryPrice
		orderId := newUUID()
		if err := h.queries.InsertPaperOrder(ctx, generated.InsertPaperOrderParams{
			Id:             orderId,
			CandidateId:    row.CandidateId,
			RiskDecisionId: row.Id,
			Symbol:         row.Symbol,
			Side:           row.Side,
			Quantity:       quantity,
			FillPrice:      row.EntryPrice,
			Status:         "OPEN",
			CreatedAt:      now,
			UpdatedAt:      now,
		}); err != nil {
			return Result{}, fmt.Errorf("order/create: inserting paper order for candidate %s: %w", row.CandidateId, err)
		}
		h.logger.InfoContext(ctx, "paper_order_created",
			slog.String("candidate_id", row.CandidateId),
			slog.String("order_id", orderId),
			slog.String("symbol", row.Symbol),
			slog.String("side", row.Side),
			slog.Float64("fill_price", row.EntryPrice),
		)
	}

	return Result{}, nil
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
