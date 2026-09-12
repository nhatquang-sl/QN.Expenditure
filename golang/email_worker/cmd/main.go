package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"

	"email_worker/internal/config"
	emailsvc "email_worker/internal/services/email"
	"email_worker/internal/worker"

	emaildb "qn.expenditure/emaildb/generated"
	shareddb "qn.expenditure/shared/database"
)

func main() {
	cfg := config.LoadJSONConfig()
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	db, err := shareddb.OpenPostgres(cfg.ConnectionStrings.PGEmail)
	if err != nil {
		logger.Error("failed to connect to database", slog.Any("error", err))
		os.Exit(1)
	}
	defer db.Close()

	sender := emailsvc.NewMailjetSender(
		cfg.Email.ApiKeyPublic,
		cfg.Email.ApiKeyPrivate,
		cfg.Email.FromEmail,
	)

	batchSize := cfg.EmailWorker.BatchSize
	if batchSize <= 0 {
		batchSize = 50
	}
	intervalSeconds := cfg.EmailWorker.IntervalSeconds
	if intervalSeconds <= 0 {
		intervalSeconds = 30
	}

	w := worker.New(db, emaildb.New(db), sender, logger)
	logger.Info("worker started",
		slog.Int("batchSize", batchSize),
		slog.Int("intervalSeconds", intervalSeconds),
	)

	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	ticker := time.NewTicker(time.Duration(intervalSeconds) * time.Second)
	defer ticker.Stop()

	// Run one tick immediately on startup, then on each interval.
	w.Tick(ctx, int32(batchSize))

	for {
		select {
		case <-ticker.C:
			w.Tick(ctx, int32(batchSize))
		case <-ctx.Done():
			logger.Info("worker shutting down")
			return
		}
	}
}
