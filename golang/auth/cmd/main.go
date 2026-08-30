package main

import (
	"auth/cmd/controllers"
	"auth/cmd/middleware"
	"auth/internal/config"
	"auth/internal/database"
	"auth/internal/services/jwt"
	redisservice "auth/internal/services/redis"
	"auth/internal/telemetry"
	"context"
	"fmt"
	"log/slog"
	"net/http"
	"os"

	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
)

func main() {
	ctx := context.Background()
	cfg := config.LoadJSONConfig()

	slogHandler, shutdown, err := telemetry.Setup(ctx, "qex-go-auth-api", cfg.Application.Version)
	if err != nil {
		slog.Error("failed to set up telemetry", slog.Any("error", err))
		os.Exit(1)
	}
	defer shutdown(ctx)

	logger := slog.New(slogHandler)
	logger.Info("config loaded", slog.String("endpoint", cfg.Application.Endpoint), slog.String("version", cfg.Application.Version))

	// connect to database
	db, queries, err := database.ConnectDB(cfg.ConnectionStrings.PGAuth)
	if err != nil {
		logger.Error("failed to connect to database", slog.Any("error", err))
		os.Exit(1)
	}
	defer db.Close()

	// 1. set up HTTP server
	mux := http.NewServeMux()

	// 2. register routes
	jwtService := jwt.NewService(cfg.Jwt)
	cache := redisservice.NewService(cfg.Redis)
	isDev := os.Getenv("APP_ENV") == "Development"
	tokenSecret := os.Getenv("TOKEN_SECRET")

	controllers.NewHealthController(mux)
	controllers.NewAuthController(mux, queries, cache, jwtService, nil, logger, tokenSecret, cfg.Application.Endpoint, isDev)

	// 3. server instance
	serverAddr := fmt.Sprintf(":%d", cfg.GoServerPort)
	srv := &http.Server{
		Addr:    serverAddr,
		Handler: otelhttp.NewHandler(middleware.Recover(logger, mux), "qex-go-auth-api"),
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
