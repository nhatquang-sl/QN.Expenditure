package logout

import (
	"context"
	"strconv"
	"time"

	"auth/internal/application"
	. "auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"
	. "auth/internal/services/redis"
)

type Command struct {
	RefreshToken string
}

type Result struct{}

type handler struct {
	db         *dbsqlc.Queries
	jwtService JwtService
	cache      *RedisService
}

func NewHandler(db *dbsqlc.Queries, jwtService JwtService, cache *RedisService) application.Handler[Command, Result] {
	return &handler{db: db, jwtService: jwtService, cache: cache}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	claims, err := h.jwtService.ValidateRefreshToken(cmd.RefreshToken)
	if err != nil || claims == nil {
		return Result{}, nil
	}

	if err := h.db.DeleteUserSessionById(ctx, claims.TokenId); err != nil {
		return Result{}, err
	}

	if ttl := time.Until(claims.ExpiresAt); ttl > 0 {
		_ = h.cache.Set(ctx, "revoked:"+strconv.FormatInt(claims.TokenId, 10), "1", ttl)
	}

	return Result{}, nil
}
