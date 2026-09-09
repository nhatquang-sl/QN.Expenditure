package httpx

import (
	"log/slog"
	"net/http"

	. "qn.expenditure/shared/apperror"
)

func Recover(logger *slog.Logger, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		defer func() {
			if rec := recover(); rec != nil {
				logger.ErrorContext(r.Context(), "panic recovered", slog.Any("error", rec))
				switch e := rec.(type) {
				case *AppError:
					WriteJSON(w, e.Code, map[string]string{"message": e.Message}, nil)
				default:
					WriteJSON(w, http.StatusInternalServerError, map[string]string{"message": "Internal Server Error"}, nil)
				}
			}
		}()
		next.ServeHTTP(w, r)
	})
}
