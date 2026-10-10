package main

import (
	"context"
	"database/sql"
	"log/slog"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	migrate "github.com/golang-migrate/migrate/v4"
	migratepostgres "github.com/golang-migrate/migrate/v4/database/postgres"
	"github.com/golang-migrate/migrate/v4/source/iofs"

	"trading_bot/internal/application/candidatetrade/find"
	"trading_bot/internal/config"
	tradingbotdb "trading_bot/internal/database"
	"trading_bot/internal/database/generated"
	"trading_bot/internal/indicator"
	"trading_bot/internal/services/kucoin"
	"trading_bot/internal/strategy/divergence"
	"trading_bot/internal/trade"

	app "qn.expenditure/shared/app"
	shareddb "qn.expenditure/shared/database"
	sharedtelemetry "qn.expenditure/shared/telemetry"
)

const (
	serviceName    = "trading-bot"
	serviceVersion = "0.0.1"
	candleLimit    = 1000
)

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	cfg := config.LoadJSONConfig()

	version := cfg.Application.Version
	if version == "" {
		version = serviceVersion
	}

	logger, shutdown, err := sharedtelemetry.Setup(ctx, version)
	if err != nil {
		slog.Error("failed to set up telemetry", slog.Any("error", err))
		os.Exit(1)
	}
	defer shutdown(context.Background())

	db, err := shareddb.OpenPostgres(cfg.ConnectionStrings.PGTrading)
	if err != nil {
		logger.Error("failed to connect to database", slog.Any("error", err))
		os.Exit(1)
	}
	defer db.Close()

	if err := runMigrations(db); err != nil {
		logger.Error("failed to run migrations", slog.Any("error", err))
		os.Exit(1)
	}

	indCfg := cfg.IndicatorConfig()
	eng := indicator.NewEngine(indCfg)
	strat := divergence.New()
	factory := trade.NewFactory()
	queries := generated.New(db)
	kucoinSvc := kucoin.NewService("https://api.kucoin.com")

	h := findcandidatetrade.NewHandler(kucoinSvc, eng, strat, factory, queries, logger)

	interval := time.Duration(cfg.TradingBot.PollIntervalSeconds) * time.Second
	logger.Info("trading bot started",
		slog.String("service", serviceName),
		slog.String("version", version),
		slog.Duration("poll_interval", interval),
		slog.Any("symbols", cfg.TradingBot.Symbols),
		slog.Any("timeframes", cfg.TradingBot.Timeframes),
	)

	tick := time.NewTicker(interval)
	defer tick.Stop()

	// Run immediately on startup, then on each tick.
	runPipeline(ctx, logger, cfg, h)

	for {
		select {
		case <-tick.C:
			runPipeline(ctx, logger, cfg, h)
		case <-ctx.Done():
			logger.Info("shutting down")
			return
		}
	}
}

func runPipeline(
	ctx context.Context,
	logger *slog.Logger,
	cfg config.AppConfig,
	h app.Handler[findcandidatetrade.Command, findcandidatetrade.Result],
) {
	var wg sync.WaitGroup
	for _, symbol := range cfg.TradingBot.Symbols {
		for _, timeframe := range cfg.TradingBot.Timeframes {
			wg.Add(1)
			go func(sym, tf string) {
				defer wg.Done()
				if _, err := h.Handle(ctx, findcandidatetrade.Command{
					Symbol:      sym,
					Timeframe:   tf,
					CandleLimit: candleLimit,
				}); err != nil {
					logger.ErrorContext(ctx, "pipeline error",
						slog.String("symbol", sym),
						slog.String("timeframe", tf),
						slog.Any("error", err),
					)
				}
			}(symbol, timeframe)
		}
	}
	wg.Wait()
}

func runMigrations(db *sql.DB) error {
	src, err := iofs.New(tradingbotdb.MigrationsFS, "migrations")
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
