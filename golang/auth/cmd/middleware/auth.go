package middleware

import (
	"context"
	"net/http"
	"strconv"

	"auth/cmd/respond"
	"auth/internal/application/apperror"
	. "auth/internal/application/shared"
	. "auth/internal/services/redis"
)

type contextKey int

const userClaimsKey contextKey = iota

func Auth(jwtService JwtService, cache *RedisService) func(http.HandlerFunc) http.HandlerFunc {
	return func(next http.HandlerFunc) http.HandlerFunc {
		return func(w http.ResponseWriter, r *http.Request) {
			cookie, err := r.Cookie("accessToken")
			if err != nil {
				respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, apperror.NewUnauthorized("missing access token"))
				return
			}
			claims, err := jwtService.ValidateAccessToken(cookie.Value)
			if err != nil || claims == nil {
				respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, apperror.NewUnauthorized("invalid access token"))
				return
			}
			key := "session:" + strconv.FormatInt(claims.TokenId, 10)
			if exists, err := cache.Exists(r.Context(), key); err == nil && !exists {
				respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, apperror.NewUnauthorized("session invalidated"))
				return
			}
			ctx := context.WithValue(r.Context(), userClaimsKey, claims)
			next(w, r.WithContext(ctx))
		}
	}
}

func UserFromContext(ctx context.Context) *UserClaims {
	claims, ok := ctx.Value(userClaimsKey).(*UserClaims)
	if !ok || claims == nil {
		panic("UserFromContext called outside of Auth middleware")
	}
	return claims
}
