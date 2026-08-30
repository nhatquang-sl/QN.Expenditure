package application

import (
	"context"
	"log/slog"

	. "auth/internal/services/redis"
)

type Cacher[C, R any] struct {
	inner Handler[C, R]
	cache *RedisService
	keyFn func(C) string
}

// NewCacher wraps inner with a Redis cache layer. Results are keyed by keyFn(cmd)
// using the service's default TTL.
func NewCacher[C, R any](inner Handler[C, R], cache *RedisService, keyFn func(C) string) Handler[C, R] {
	return &Cacher[C, R]{inner: inner, cache: cache, keyFn: keyFn}
}

func (c *Cacher[C, R]) Handle(ctx context.Context, cmd C) (R, error) {
	key := c.keyFn(cmd)
	missed := false
	result, err := c.cache.GetOrCreateDefault(ctx, key, func() (R, error) {
		missed = true
		return c.inner.Handle(ctx, cmd)
	})
	if err == nil {
		if missed {
			slog.Default().InfoContext(ctx, "cache miss", slog.String("key", key))
		} else {
			slog.Default().InfoContext(ctx, "cache hit", slog.String("key", key))
		}
	}
	return result, err
}
