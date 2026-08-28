package middleware

import (
	"context"
	"net/http"

	"auth/cmd/respond"
	"auth/internal/application/apperror"
	. "auth/internal/application/shared"
)

type contextKey int

const userClaimsKey contextKey = iota

func Auth(jwtService JwtService) func(http.HandlerFunc) http.HandlerFunc {
	return func(next http.HandlerFunc) http.HandlerFunc {
		return func(w http.ResponseWriter, r *http.Request) {
			cookie, err := r.Cookie("accessToken")
			if err != nil {
				respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, apperror.NewUnauthorized("missing access token"))
				return
			}
			claims, err := jwtService.ValidateAccessToken(cookie.Value)
			if err != nil {
				respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, apperror.NewUnauthorized("invalid access token"))
				return
			}
			ctx := context.WithValue(r.Context(), userClaimsKey, claims)
			next(w, r.WithContext(ctx))
		}
	}
}

func UserFromContext(ctx context.Context) (*UserClaims, error) {
	claims, ok := ctx.Value(userClaimsKey).(*UserClaims)
	if !ok || claims == nil {
		return nil, apperror.NewUnauthorized("unauthenticated")
	}
	return claims, nil
}
