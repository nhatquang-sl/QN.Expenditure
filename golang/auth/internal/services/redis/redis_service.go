package service

import (
	"context"
	"encoding/json"
	"errors"
	"time"

	. "auth/internal/config"

	"github.com/redis/go-redis/v9"
)

type RedisService struct {
	client     *redis.Client
	defaultTTL time.Duration
}

func NewService(cfg RedisConfig) *RedisService {
	client := redis.NewClient(&redis.Options{
		Addr:     cfg.Addr,
		Password: cfg.Password,
	})
	return &RedisService{client: client, defaultTTL: time.Duration(cfg.DefaultTTLSeconds) * time.Second}
}

// GetOrCreate gets the value associated with key if it exists, or generates a new entry
// using the provided factory if the key is not found, stores it with the given TTL, and returns it.
func (s *RedisService) GetOrCreate[T any](ctx context.Context, key string, factory func() (T, error), ttl time.Duration) (T, error) {
	var zero T

	data, err := s.client.Get(ctx, key).Bytes()
	if err == nil {
		var result T
		if err := json.Unmarshal(data, &result); err != nil {
			return zero, err
		}
		return result, nil
	}
	if !errors.Is(err, redis.Nil) {
		return zero, err
	}

	value, err := factory()
	if err != nil {
		return zero, err
	}

	data, err = json.Marshal(value)
	if err != nil {
		return zero, err
	}

	if err := s.client.Set(ctx, key, data, ttl).Err(); err != nil {
		return zero, err
	}

	return value, nil
}

// GetOrCreateDefault is like GetOrCreate but uses the DefaultTTL configured in RedisConfig.
func (s *RedisService) GetOrCreateDefault[T any](ctx context.Context, key string, factory func() (T, error)) (T, error) {
	return s.GetOrCreate(ctx, key, factory, s.defaultTTL)
}

func (s *RedisService) Set(ctx context.Context, key, value string, ttl time.Duration) error {
	return s.client.Set(ctx, key, value, ttl).Err()
}

func (s *RedisService) Exists(ctx context.Context, key string) (bool, error) {
	n, err := s.client.Exists(ctx, key).Result()
	return n > 0, err
}

func (s *RedisService) Delete(ctx context.Context, key string) error {
	return s.client.Del(ctx, key).Err()
}
