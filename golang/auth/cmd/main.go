package main

import (
	"log/slog"
	"os"

	"auth/internal/config"
)

func main() {
	logger := slog.New(slog.NewTextHandler(os.Stdout, nil))
	cfg := config.LoadJSONConfig()

	logger.Info("config loaded successfully", slog.String("env", cfg.Application.Endpoint), slog.String("version", cfg.Application.Version))
	// srv := server.NewServer(cfg, logger)
	// if cfg.TLSCertPath != "" && cfg.TLSKeyPath != "" {
	// 	logger.Info("starting server (TLS)", slog.String("addr", srv.Addr), slog.String("env", cfg.Application.Endpoint))
	// 	if err := srv.ListenAndServeTLS(cfg.TLSCertPath, cfg.TLSKeyPath); err != nil {
	// 		logger.Error("server stopped", slog.Any("error", err))
	// 		os.Exit(1)
	// 	}
	// } else {
	// 	logger.Info("starting server", slog.String("addr", srv.Addr), slog.String("env", cfg.Application.Endpoint))
	// 	if err := srv.ListenAndServe(); err != nil {
	// 		logger.Error("server stopped", slog.Any("error", err))
	// 		os.Exit(1)
	// 	}
	// }
}
