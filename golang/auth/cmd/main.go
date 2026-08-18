package main

import (
	"auth/cmd/controllers"
	"auth/cmd/middleware"
	"auth/internal/config"
	"auth/internal/database"
	"auth/internal/services/jwt"
	"fmt"
	"log/slog"
	"net/http"
	"os"
)

func main() {
	logger := slog.New(slog.NewTextHandler(os.Stdout, nil))
	cfg := config.LoadJSONConfig()
	logger.Info("config loaded", slog.String("endpoint", cfg.Application.Endpoint), slog.String("version", cfg.Application.Version))

	// connect to database
	queries := database.ConnectDB("host=localhost user=sunlight password=@dmin191092 dbname=auth-test sslmode=disable")

	// 1. set up HTTP server
	mux := http.NewServeMux()

	// 2. register routes
	jwtService := jwt.NewService(cfg.Jwt)
	isDev := os.Getenv("APP_ENV") == "Development"
	tokenSecret := os.Getenv("TOKEN_SECRET")

	controllers.NewHealthController(mux)
	controllers.NewAuthController(mux, queries, jwtService, nil, logger, tokenSecret, cfg.Application.Endpoint, isDev)

	// 3. server instance
	serverAddr := fmt.Sprintf(":%d", cfg.GoServerPort)
	srv := &http.Server{
		Addr:    serverAddr,
		Handler: middleware.Recover(mux),
	}

	if cfg.TLSCertPath != "" && cfg.TLSKeyPath != "" {
		logger.Info("starting server (TLS)", slog.String("addr", "https://localhost"+srv.Addr))
		if err := srv.ListenAndServeTLS(cfg.TLSCertPath, cfg.TLSKeyPath); err != nil {
			logger.Error("server stopped", slog.Any("error", err))
			os.Exit(1)
		}
	} else {
		logger.Info("starting server", slog.String("addr", srv.Addr))
		if err := srv.ListenAndServe(); err != nil {
			logger.Error("server stopped", slog.Any("error", err))
			os.Exit(1)
		}
	}
}
