package getportfoliosnapshot

import (
	"context"
	"fmt"
	"log/slog"

	"trading_bot/internal/database/generated"

	app "qn.expenditure/shared/app"
)

// Command carries no inputs — the snapshot is derived from current DB state.
type Command struct{}

// Result is the portfolio state at the moment Handle is called.
type Result struct {
	Equity        float64
	AvailableCash float64
	TotalExposure float64
	UnrealizedPnl float64
	OpenPositions int
}

type handler struct {
	queries *generated.Queries
	logger  *slog.Logger
}

// NewHandler returns a Handler that reads account_balances and positions from the
// DB and returns a computed portfolio snapshot.
func NewHandler(queries *generated.Queries, logger *slog.Logger) app.Handler[Command, Result] {
	return &handler{queries: queries, logger: logger}
}

func (h *handler) Handle(ctx context.Context, _ Command) (Result, error) {
	balances, err := h.queries.GetAccountBalances(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("getportfoliosnapshot: fetching account balances: %w", err)
	}

	positions, err := h.queries.GetOpenPositions(ctx)
	if err != nil {
		return Result{}, fmt.Errorf("getportfoliosnapshot: fetching open positions: %w", err)
	}

	return computeSnapshot(balances, positions), nil
}

// computeSnapshot derives a Result from raw DB rows. Pure function — no I/O.
func computeSnapshot(balances []generated.AccountBalance, positions []generated.Position) Result {
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

	return Result{
		Equity:        availableCash + totalExposure,
		AvailableCash: availableCash,
		TotalExposure: totalExposure,
		UnrealizedPnl: unrealizedPnl,
		OpenPositions: len(positions),
	}
}
