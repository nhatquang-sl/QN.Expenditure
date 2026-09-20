package main

import (
	"context"
	"crypto/tls"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"auth_bot/internal/bot"
	"auth_bot/internal/config"

	shareddb "qn.expenditure/shared/database"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	cfg := config.LoadJSONConfig()

	registerInterval := cfg.AuthBot.RegisterIntervalSeconds
	if registerInterval == 0 {
		registerInterval = 450 // 450 s ≈ 7.5 min — maximises a 6,000-email/month quota without exceeding it in any calendar month
	}
	loginInterval := cfg.AuthBot.LoginIntervalSeconds
	if loginInterval <= 0 {
		loginInterval = 30
	}

	httpClient := &http.Client{
		Timeout: 25 * time.Second,
		Transport: &http.Transport{
			TLSClientConfig: &tls.Config{InsecureSkipVerify: true}, //nolint:gosec // internal Docker network, self-signed cert
		},
	}
	b := bot.New(cfg.AuthBot.AuthBaseUrl, cfg.AuthBot.BotPassword, httpClient, logger)

	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	logger.Info("auth_bot started",
		slog.String("authBaseUrl", cfg.AuthBot.AuthBaseUrl),
		slog.Int("registerIntervalSeconds", registerInterval),
		slog.Int("loginIntervalSeconds", loginInterval),
	)

	// Seed from DB so existing bot users survive restarts.
	if cfg.AuthBot.PGAuthConnection != "" {
		db, err := shareddb.OpenPostgres(cfg.AuthBot.PGAuthConnection)
		if err != nil {
			logger.Error("failed to connect to auth db for seeding", slog.Any("error", err))
		} else {
			b.Seed(ctx, db)
			db.Close()
		}
	}

	// Register ticker — nil channel never fires, so register is effectively disabled when registerInterval < 0.
	var registerC <-chan time.Time
	if registerInterval > 0 {
		b.Register(ctx) // one immediate registration before the ticker starts
		t := time.NewTicker(time.Duration(registerInterval) * time.Second)
		defer t.Stop()
		registerC = t.C
	}

	loginTicker := time.NewTicker(time.Duration(loginInterval) * time.Second)
	defer loginTicker.Stop()

	for {
		select {
		case <-registerC:
			b.Register(ctx)
		case <-loginTicker.C:
			b.Login(ctx)
		case <-ctx.Done():
			logger.Info("auth_bot shutting down")
			return
		}
	}
}
