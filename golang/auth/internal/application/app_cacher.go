package application

import (
	"context"

	. "auth/internal/services/redis"
)

type Cacher[C, R any] struct {
	inner Handler[C, R]
	cache *RedisService
	keyFn func(C) string
}

// NewCacher wraps inner with a Redis cache layer. Results are keyed by keyFn(cmd)
// using the service's default TTL. If cache is nil, inner is returned unwrapped.
func NewCacher[C, R any](inner Handler[C, R], cache *RedisService, keyFn func(C) string) Handler[C, R] {
	if cache == nil {
		return inner
	}
	return &Cacher[C, R]{inner: inner, cache: cache, keyFn: keyFn}
}

func (c *Cacher[C, R]) Handle(ctx context.Context, cmd C) (R, error) {
	return c.cache.GetOrCreateDefault(ctx, c.keyFn(cmd), func() (R, error) {
		return c.inner.Handle(ctx, cmd)
	})
}
