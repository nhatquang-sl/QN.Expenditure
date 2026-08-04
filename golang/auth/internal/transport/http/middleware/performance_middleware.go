package middleware

import (
	"log/slog"
	"net/http"
	"time"
)

func Performance(logger *slog.Logger) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			start := time.Now()
			next.ServeHTTP(w, r)
			elapsed := time.Since(start)

			level := slog.LevelInfo
			if elapsed > 500*time.Millisecond {
				level = slog.LevelWarn
			}
			logger.Log(r.Context(), level, "request processed",
				slog.String("method", r.Method),
				slog.String("path", r.URL.Path),
				slog.Duration("elapsed", elapsed),
			)
		})
	}
}
