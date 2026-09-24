package main

import (
	"context"
	"database/sql"
	"log"
	"log/slog"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	_ "github.com/lib/pq"
	"github.com/golang-migrate/migrate/v4"
	"github.com/golang-migrate/migrate/v4/database/postgres"
	_ "github.com/golang-migrate/migrate/v4/source/file"

	"trading_bot/internal/indicator"
	"trading_bot/internal/kucoin"
	"trading_bot/internal/marketdata"
	"trading_bot/internal/strategy"
	"trading_bot/internal/strategy/divergence"
	"trading_bot/internal/trade"
)

const (
	serviceName    = "trading-bot"
	serviceVersion = "0.0.1"
	candleLimit    = 1000
)

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	cfg, err := loadConfig()
	if err != nil {
		log.Fatalf("config: %v", err)
	}

	logger := slog.New(slog.NewTextHandler(os.Stdout, nil))

	db, err := openDB(cfg.DBDSN)
	if err != nil {
		log.Fatalf("db: %v", err)
	}
	defer db.Close()

	if err := runMigrations(db, cfg.DBDSN); err != nil {
		log.Fatalf("migrations: %v", err)
	}

	indCfg := cfg.indicatorConfig()
	eng := indicator.NewEngine(indCfg)
	strat := divergence.New()
	factory := trade.NewFactory()
	repo := trade.NewRepository(db, logger)
	candleRepo := marketdata.CandleRepository(kucoin.NewAdapter(cfg.KuCoinBaseURL))

	interval := time.Duration(cfg.PollIntervalSeconds) * time.Second
	logger.Info("trading bot started",
		slog.String("service", serviceName),
		slog.String("version", serviceVersion),
		slog.Duration("poll_interval", interval),
		slog.Any("symbols", cfg.Symbols),
		slog.Any("timeframes", cfg.Timeframes),
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
	cfg AppConfig,
	candleRepo marketdata.CandleRepository,
	eng indicator.Engine,
	strat strategy.Strategy,
	factory *trade.Factory,
	repo *trade.Repository,
) {
	var wg sync.WaitGroup
	for _, symbol := range cfg.Symbols {
		for _, timeframe := range cfg.Timeframes {
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

func openDB(dsn string) (*sql.DB, error) {
	db, err := sql.Open("postgres", dsn)
	if err != nil {
		return nil, err
	}
	db.SetMaxOpenConns(25)
	db.SetMaxIdleConns(5)
	db.SetConnMaxLifetime(30 * time.Minute)
	db.SetConnMaxIdleTime(5 * time.Minute)
	if err := db.Ping(); err != nil {
		return nil, err
	}
	return db, nil
}

func runMigrations(db *sql.DB, dsn string) error {
	driver, err := postgres.WithInstance(db, &postgres.Config{})
	if err != nil {
		return err
	}
	m, err := migrate.NewWithDatabaseInstance("file://internal/database/migrations", "postgres", driver)
	if err != nil {
		return err
	}
	if err := m.Up(); err != nil && err != migrate.ErrNoChange {
		return err
	}
	return nil
}
