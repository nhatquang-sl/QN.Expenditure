package main

import (
	"context"
	"database/sql"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"time"

	sessioncleaner "auth/internal/application/session_cleaner"
	"auth/cmd/controllers"
	"auth/cmd/middleware"
	"auth/internal/config"
	authdb "auth/internal/database"
	dbsqlc "auth/internal/database/generated"
	"auth/internal/services/jwt"
	redisservice "auth/internal/services/redis"
	"auth/internal/telemetry"

	migrate "github.com/golang-migrate/migrate/v4"
	migratepostgres "github.com/golang-migrate/migrate/v4/database/postgres"
	"github.com/golang-migrate/migrate/v4/source/iofs"
	. "qn.expenditure/shared/database"

	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
)

func main() {
	ctx := context.Background()
	cfg := config.LoadJSONConfig()

	slogHandler, shutdown, err := telemetry.Setup(ctx, cfg.Application.Version)
	if err != nil {
		slog.Error("failed to set up telemetry", slog.Any("error", err))
		os.Exit(1)
	}
	defer shutdown(ctx)

	logger := slog.New(slogHandler)
	slog.SetDefault(logger)
	logger.Info("config loaded", slog.String("endpoint", cfg.Application.Endpoint), slog.String("version", cfg.Application.Version))

	// connect to database
	db, err := OpenPostgres(cfg.ConnectionStrings.PGAuth)
	if err != nil {
		logger.Error("failed to connect to database", slog.Any("error", err))
		os.Exit(1)
	}
	defer db.Close()

	if err := runMigrations(db); err != nil {
		logger.Error("failed to run migrations", slog.Any("error", err))
		os.Exit(1)
	}

	queries := dbsqlc.New(db)

	// start session cleaner: runs immediately on startup, then every hour
	cleaner := sessioncleaner.NewHandler(queries, logger)
	go func() {
		cleaner.Handle(ctx, sessioncleaner.Command{})
		ticker := time.NewTicker(time.Hour)
		defer ticker.Stop()
		for {
			select {
			case <-ticker.C:
				cleaner.Handle(ctx, sessioncleaner.Command{})
			case <-ctx.Done():
				return
			}
		}
	}()

	// 1. set up HTTP server
	mux := http.NewServeMux()

	// 2. register routes
	jwtService := jwt.NewService(cfg.Jwt)
	cache := redisservice.NewService(cfg.Redis)

	isDev := os.Getenv("APP_ENV") == "Development"
	tokenSecret := os.Getenv("TOKEN_SECRET")

	controllers.NewHealthController(mux, logger)
	controllers.NewAuthController(mux, &cfg, queries, cache, jwtService, logger, tokenSecret, isDev)

	// 3. server instance
	serverAddr := fmt.Sprintf(":%d", cfg.GoServerPort)
	srv := &http.Server{
		Addr:    serverAddr,
		Handler: otelhttp.NewHandler(middleware.Recover(logger, mux), os.Getenv("OTEL_SERVICE_NAME")),
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

func runMigrations(db *sql.DB) error {
	src, err := iofs.New(authdb.MigrationsFS, "migrations")
	if err != nil {
		return err
	}
	driver, err := migratepostgres.WithInstance(db, &migratepostgres.Config{})
	if err != nil {
		return err
	}
	m, err := migrate.NewWithInstance("iofs", src, "postgres", driver)
	if err != nil {
		return err
	}
	if err := m.Up(); err != nil && err != migrate.ErrNoChange {
		return err
	}
	return nil
}
