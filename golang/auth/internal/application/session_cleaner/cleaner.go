package sessioncleaner

import (
	"context"
	"log/slog"

	dbsqlc "auth/internal/database/generated"

	. "qn.expenditure/shared/app"
)

type Command struct{}

type Result struct{}

type handler struct {
	db     *dbsqlc.Queries
	logger *slog.Logger
}

func NewHandler(db *dbsqlc.Queries, logger *slog.Logger) Handler[Command, Result] {
	return &handler{db: db, logger: logger}
}

func (h *handler) Handle(ctx context.Context, _ Command) (Result, error) {
	n, err := h.db.DeleteStaleSessionHistories(ctx)
	if err != nil {
		h.logger.Error("session cleaner: failed to delete stale session histories", slog.Any("error", err))
	} else {
		h.logger.Info("session cleaner: deleted stale session histories", slog.Int64("count", n))
	}

	n, err = h.db.DeleteStaleSessions(ctx)
	if err != nil {
		h.logger.Error("session cleaner: failed to delete stale sessions", slog.Any("error", err))
	} else {
		h.logger.Info("session cleaner: deleted stale sessions", slog.Int64("count", n))
	}

	return Result{}, nil
}
