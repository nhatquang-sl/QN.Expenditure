package logout

import (
	"context"
	"strconv"

	"auth/internal/application"
	dbsqlc "auth/internal/database/generated"
	. "auth/internal/services/redis"
)

type Command struct {
	TokenId int64
}

type Result struct{}

type handler struct {
	db    *dbsqlc.Queries
	cache *RedisService
}

func NewHandler(db *dbsqlc.Queries, cache *RedisService) application.Handler[Command, Result] {
	return &handler{db: db, cache: cache}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	if err := h.db.DeleteLoginHistoryById(ctx, cmd.TokenId); err != nil {
		return Result{}, err
	}
	_ = h.cache.Delete(ctx, "session:"+strconv.FormatInt(cmd.TokenId, 10))
	return Result{}, nil
}
