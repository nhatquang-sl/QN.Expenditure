package middleware

import (
	"log/slog"
	"net/http"

	"auth/cmd/respond"
	"auth/internal/application/apperror"
)

func Recover(logger *slog.Logger, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		defer func() {
			if rec := recover(); rec != nil {
				logger.ErrorContext(r.Context(), "panic recovered", slog.Any("error", rec))
				writePanic(w, rec)
			}
		}()
		next.ServeHTTP(w, r)
	})
}

func writePanic(w http.ResponseWriter, rec any) {
	res := respond.NewResponse(w)
	switch e := rec.(type) {
	case *apperror.AppError:
		res.JSON(e.Code, map[string]string{"message": e.Message}, nil)
	default:
		res.JSON(http.StatusInternalServerError, map[string]string{"message": "Internal Server Error"}, nil)
	}
}
