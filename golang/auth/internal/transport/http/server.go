package server

import (
	"log/slog"
	"net/http"

	"auth/internal/infrastructure/config"
	"auth/internal/transport/http/handler"
	"auth/internal/transport/http/middleware"
)

func NewServer(cfg config.Config, logger *slog.Logger) *http.Server {
	mux := http.NewServeMux()

	handler.RegisterHealthRoutes(mux, cfg)

	var h http.Handler = mux
	h = middleware.Performance(logger)(h)
	h = middleware.Recovery(logger)(h)

	return &http.Server{
		Addr:    ":" + cfg.Port,
		Handler: h,
	}
}
