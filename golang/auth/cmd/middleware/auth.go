package middleware

import (
	"context"
	"net/http"

	"auth/internal/application/apperror"
	"auth/internal/application/shared"
)

type contextKey int

const userClaimsKey contextKey = iota

func Auth(jwtService shared.JwtService) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			cookie, err := r.Cookie("accessToken")
			if err != nil {
				panic(apperror.NewUnauthorized("missing access token"))
			}
			claims, err := jwtService.ValidateAccessToken(cookie.Value)
			if err != nil {
				panic(apperror.NewUnauthorized("invalid access token"))
			}
			ctx := context.WithValue(r.Context(), userClaimsKey, claims)
			next.ServeHTTP(w, r.WithContext(ctx))
		})
	}
}

func UserFromContext(ctx context.Context) *shared.UserClaims {
	claims, ok := ctx.Value(userClaimsKey).(*shared.UserClaims)
	if !ok || claims == nil {
		panic(apperror.NewUnauthorized("unauthenticated"))
	}
	return claims
}
