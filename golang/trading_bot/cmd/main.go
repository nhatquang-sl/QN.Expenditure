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

	"trading_bot/internal/config"
	tradingbotdb "trading_bot/internal/database"
	"trading_bot/internal/indicator"
	"trading_bot/internal/kucoin"
	"trading_bot/internal/marketdata"
	"trading_bot/internal/strategy"
	"trading_bot/internal/strategy/divergence"
	"trading_bot/internal/trade"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"

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
	repo := trade.NewRepository(db, logger)
	candleRepo := marketdata.CandleRepository(kucoin.NewAdapter("https://api.kucoin.com"))

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
	runPipeline(ctx, logger, cfg, candleRepo, eng, strat, factory, repo)

	for {
		select {
		case <-tick.C:
			runPipeline(ctx, logger, cfg, candleRepo, eng, strat, factory, repo)
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
	candleRepo marketdata.CandleRepository,
	eng indicator.Engine,
	strat strategy.Strategy,
	factory *trade.Factory,
	repo *trade.Repository,
) {
	var wg sync.WaitGroup
	for _, symbol := range cfg.TradingBot.Symbols {
		for _, timeframe := range cfg.TradingBot.Timeframes {
			wg.Add(1)
			go func(sym, tf string) {
				defer wg.Done()
				if err := processPair(ctx, logger, sym, tf, candleRepo, eng, strat, factory, repo); err != nil {
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

func processPair(
	ctx context.Context,
	logger *slog.Logger,
	symbol, timeframe string,
	candleRepo marketdata.CandleRepository,
	eng indicator.Engine,
	strat strategy.Strategy,
	factory *trade.Factory,
	repo *trade.Repository,
) error {
	ctx, span := otel.Tracer(serviceName).Start(ctx, "processPair")
	defer span.End()
	span.SetAttributes(
		attribute.String("symbol", symbol),
		attribute.String("timeframe", timeframe),
		attribute.Int("candle_limit", candleLimit),
	)

	logger = logger.With(slog.String("symbol", symbol), slog.String("timeframe", timeframe))
	logger.InfoContext(ctx, "processing pair", slog.Int("candle_limit", candleLimit))

	candles, err := candleRepo.GetClosedCandles(ctx, symbol, timeframe, candleLimit)
	if err != nil {
		return err
	}
	if len(candles) == 0 {
		return nil
	}

	snap, err := eng.Calculate(candles)
	if err != nil {
		return err
	}

	lastCandle := candles[len(candles)-1]
	sig, err := strat.Evaluate(strategy.MarketContext{Candle: lastCandle, Indicators: snap})
	if err != nil {
		return err
	}
	if sig == nil {
		return nil
	}

	ct := factory.Create(lastCandle, snap, sig, divergence.StrategyName, divergence.StrategyVersion)
	if ct == nil {
		return nil // in-memory duplicate within this process run
	}

	return repo.Save(ctx, ct)
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
