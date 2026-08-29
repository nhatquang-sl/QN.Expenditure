package logout

import (
	"context"

	"auth/internal/application"
	dbsqlc "auth/internal/database/generated"
)

type Command struct {
	TokenId int64
}

type Result struct{}

type handler struct {
	db *dbsqlc.Queries
}

func NewHandler(db *dbsqlc.Queries) application.Handler[Command, Result] {
	return &handler{db: db}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	if err := h.db.DeleteLoginHistoryById(ctx, cmd.TokenId); err != nil {
		return Result{}, err
	}
	return Result{}, nil
}
