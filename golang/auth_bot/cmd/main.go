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
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	cfg := config.LoadJSONConfig()

	registerInterval := cfg.AuthBot.RegisterIntervalSeconds
	if registerInterval <= 0 {
		registerInterval = 60
	}
	loginInterval := cfg.AuthBot.LoginIntervalSeconds
	if loginInterval <= 0 {
		loginInterval = 30
	}

	httpClient := &http.Client{
		Timeout: 10 * time.Second,
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

	// Run one register immediately so at least one user is available for the login loop.
	b.Register(ctx)

	registerTicker := time.NewTicker(time.Duration(registerInterval) * time.Second)
	defer registerTicker.Stop()

	for {
		select {
		case <-registerTicker.C:
			b.Register(ctx)
		case <-ctx.Done():
			logger.Info("auth_bot shutting down")
			return
		}
	}
}
