package syncportfolio

import (
	"context"
	"crypto/rand"
	"fmt"
	"log/slog"
	"time"

	"trading_bot/internal/database/generated"
	"trading_bot/internal/services/kucoin"

	app "qn.expenditure/shared/app"
)

// Command carries no inputs — the sync always fetches the full current state
// from KuCoin and overwrites the local account_balances table.
type Command struct{}

// Result reports what was synced.
type Result struct {
	BalancesSynced int
	OpenOrders     int
}

type handler struct {
	kucoinSvc *kucoin.Service
	queries   *generated.Queries
	logger    *slog.Logger
}

// NewHandler returns a Handler that syncs account balances from KuCoin into the DB.
func NewHandler(
	kucoinSvc *kucoin.Service,
	queries *generated.Queries,
	logger *slog.Logger,
) app.Handler[Command, Result] {
	return &handler{
		kucoinSvc: kucoinSvc,
		queries:   queries,
		logger:    logger,
	}
}

func (h *handler) Handle(ctx context.Context, _ Command) (Result, error) {
	balances, err := h.kucoinSvc.GetBalances(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("syncportfolio: fetching balances from exchange: %w", err)
	}

	if err := h.queries.DeleteAllAccountBalances(ctx); err != nil {
		return Result{}, fmt.Errorf("syncportfolio: clearing account balances: %w", err)
	}

	now := time.Now().UTC()
	for _, b := range balances {
		if err := h.queries.UpsertAccountBalance(ctx, generated.UpsertAccountBalanceParams{
			Id:        newUUID(),
			Asset:     b.Asset,
			Total:     b.Total,
			Available: b.Available,
			Locked:    b.Locked,
			UpdatedAt: now,
		}); err != nil {
			return Result{}, fmt.Errorf("syncportfolio: upserting balance for %s: %w", b.Asset, err)
		}
	}

	// Fetch open orders for observability. They are not separately persisted —
	// their locked-fund effect is already reflected in the balance data from KuCoin.
	var openOrderCount int
	orders, err := h.kucoinSvc.GetOpenOrders(ctx)
	if err != nil {
		h.logger.WarnContext(ctx, "syncportfolio: failed to fetch open orders, skipping",
			slog.Any("error", err))
	} else {
		openOrderCount = len(orders)
	}

	h.logger.InfoContext(ctx, "portfolio_synced",
		slog.Int("balances_synced", len(balances)),
		slog.Int("open_orders", openOrderCount),
	)

	return Result{
		BalancesSynced: len(balances),
		OpenOrders:     openOrderCount,
	}, nil
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
