package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"strings"
	"sync"
	"syscall"
	"time"

	migrate "github.com/golang-migrate/migrate/v4"
	migratepostgres "github.com/golang-migrate/migrate/v4/database/postgres"
	"github.com/golang-migrate/migrate/v4/source/iofs"

	findcandidatetrade "trading_bot/internal/application/candidatetrade/find"
	orderclose "trading_bot/internal/application/order/close"
	ordercreate "trading_bot/internal/application/order/create"
	orderfill "trading_bot/internal/application/order/fill"
	riskevaluate "trading_bot/internal/application/risk/evaluate"
	syncportfolio "trading_bot/internal/application/portfolio/sync"
	"trading_bot/internal/config"
	tradingbotdb "trading_bot/internal/database"
	"trading_bot/internal/database/generated"
	"trading_bot/internal/indicator"
	"trading_bot/internal/services/kucoin"
	"trading_bot/internal/strategy/divergence"

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

	// --- Dependency wiring ---

	queries := generated.New(db)
	kucoinSvc := kucoin.NewService(cfg.KuCoin)

	// Portfolio sync from exchange on startup (non-fatal).
	syncHandler := syncportfolio.NewHandler(kucoinSvc, queries, logger)
	if _, err := syncHandler.Handle(ctx, syncportfolio.Command{}); err != nil {
		logger.Warn("portfolio sync failed on startup, continuing with empty portfolio",
			slog.Any("error", err))
	}

	// Candidate find handler.
	indCfg := cfg.IndicatorConfig()
	eng := indicator.NewEngine(indCfg)
	strat := divergence.New()
	findHandler := findcandidatetrade.NewHandler(kucoinSvc, eng, strat, queries, logger)

	// Risk evaluate handler.
	gate := riskevaluate.NewHardRiskGate()
	riskEvalHandler := riskevaluate.NewHandler(queries, gate, cfg.RiskLimits, logger)

	// Order lifecycle handlers.
	orderCreateHandler := ordercreate.NewHandler(queries, logger)
	orderFillHandler := orderfill.NewHandler(queries, kucoinSvc, logger)
	orderCloseHandler := orderclose.NewHandler(queries, kucoinSvc, logger)

	interval := time.Duration(cfg.TradingBot.PollIntervalSeconds) * time.Second
	logger.Info("trading bot started",
		slog.String("service", serviceName),
		slog.String("version", version),
		slog.Duration("poll_interval", interval),
		slog.Any("symbols", cfg.TradingBot.Symbols),
		slog.Any("timeframes", cfg.TradingBot.Timeframes),
		slog.Int("api_port", cfg.TradingBot.APIPort),
	)

	// Start HTTP API server in background.
	srv := startAPIServer(ctx, cfg.TradingBot.APIPort, orderCloseHandler, logger)
	defer srv.Shutdown(context.Background())

	tick := time.NewTicker(interval)
	defer tick.Stop()

	// Run immediately on startup, then on each tick.
	runPipeline(ctx, logger, cfg, findHandler, riskEvalHandler, orderCreateHandler, orderFillHandler)

	for {
		select {
		case <-tick.C:
			runPipeline(ctx, logger, cfg, findHandler, riskEvalHandler, orderCreateHandler, orderFillHandler)
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
	findHandler app.Handler[findcandidatetrade.Command, findcandidatetrade.Result],
	riskEvalHandler app.Handler[riskevaluate.Command, riskevaluate.Result],
	orderCreateHandler app.Handler[ordercreate.Command, ordercreate.Result],
	orderFillHandler app.Handler[orderfill.Command, orderfill.Result],
) {
	// Per symbol×timeframe: find candidate → risk evaluate.
	var wg sync.WaitGroup
	for _, symbol := range cfg.TradingBot.Symbols {
		for _, timeframe := range cfg.TradingBot.Timeframes {
			wg.Add(1)
			go func(sym, tf string) {
				defer wg.Done()

				findResult, err := findHandler.Handle(ctx, findcandidatetrade.Command{
					Symbol:      sym,
					Timeframe:   tf,
					CandleLimit: candleLimit,
				})
				if err != nil {
					logger.ErrorContext(ctx, "find pipeline error",
						slog.String("symbol", sym),
						slog.String("timeframe", tf),
						slog.Any("error", err),
					)
					return
				}

				if findResult.TradeId == "" {
					return // no new candidate — nothing to evaluate
				}

				if _, err := riskEvalHandler.Handle(ctx, riskevaluate.Command{
					TradeId: findResult.TradeId,
				}); err != nil {
					logger.ErrorContext(ctx, "risk evaluate pipeline error",
						slog.String("symbol", sym),
						slog.String("timeframe", tf),
						slog.String("trade_id", findResult.TradeId),
						slog.Any("error", err),
					)
				}
			}(symbol, timeframe)
		}
	}
	wg.Wait()

	// Once per tick: create orders for newly approved decisions, then check fills.
	if _, err := orderCreateHandler.Handle(ctx, ordercreate.Command{}); err != nil {
		logger.ErrorContext(ctx, "order/create pipeline error", slog.Any("error", err))
	}
	if _, err := orderFillHandler.Handle(ctx, orderfill.Command{}); err != nil {
		logger.ErrorContext(ctx, "order/fill pipeline error", slog.Any("error", err))
	}
}

// startAPIServer launches the HTTP server in a background goroutine and returns
// the *http.Server so the caller can shut it down gracefully.
func startAPIServer(
	ctx context.Context,
	port int,
	closeHandler app.Handler[orderclose.Command, orderclose.Result],
	logger *slog.Logger,
) *http.Server {
	mux := http.NewServeMux()

	// POST /orders/{paper_order_id}/close
	mux.HandleFunc("/orders/", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}

		// Extract paper_order_id from path: /orders/{id}/close
		path := strings.TrimPrefix(r.URL.Path, "/orders/")
		path = strings.TrimSuffix(path, "/close")
		paperOrderId := strings.TrimSpace(path)
		if paperOrderId == "" || !strings.HasSuffix(r.URL.Path, "/close") {
			http.Error(w, "invalid path — expected /orders/{id}/close", http.StatusBadRequest)
			return
		}

		result, err := closeHandler.Handle(r.Context(), orderclose.Command{PaperOrderId: paperOrderId})
		if err != nil {
			logger.ErrorContext(r.Context(), "order/close error",
				slog.String("paper_order_id", paperOrderId),
				slog.Any("error", err),
			)
			http.Error(w, fmt.Sprintf("close failed: %v", err), http.StatusInternalServerError)
			return
		}

		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]float64{"net_pnl": result.NetPnL})
	})

	srv := &http.Server{
		Addr:    fmt.Sprintf(":%d", port),
		Handler: mux,
	}

	go func() {
		logger.Info("API server listening", slog.Int("port", port))
		if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			logger.Error("API server error", slog.Any("error", err))
		}
	}()

	// Shut down when main context is cancelled.
	go func() {
		<-ctx.Done()
		srv.Shutdown(context.Background())
	}()

	return srv
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
