package main

import (
	"context"
	"encoding/json"
	"errors"
	"log/slog"
	"os"
	"os/signal"
	"syscall"

	insertemailqueue "email_consumer/internal/application/email_queue/insert_email_queue"
	"email_consumer/internal/config"
	"email_consumer/internal/telemetry"

	emaildb "qn.expenditure/emaildb/generated"
	shareddb "qn.expenditure/shared/database"

	. "qn.expenditure/shared/app"
	. "qn.expenditure/shared/apperror"
	. "qn.expenditure/shared/messaging"
	. "qn.expenditure/shared/messaging/messages"

	amqp "github.com/rabbitmq/amqp091-go"
)

func handleDelivery(ctx context.Context, d amqp.Delivery, handler Handler[insertemailqueue.Command, insertemailqueue.Result], logger *slog.Logger) {
	var msg EmailMessage
	if err := json.Unmarshal(d.Body, &msg); err != nil {
		logger.Warn("invalid message body, discarding", slog.Any("error", err))
		return
	}

	var cmd insertemailqueue.Command
	if err := cmd.FromEmailMessage(msg); err != nil {
		logger.Warn("invalid email message, discarding", slog.Any("error", err))
		return
	}

	_, err := handler.Handle(ctx, cmd)
	if err != nil {
		var appErr *AppError
		if errors.As(err, &appErr) {
			logger.Warn("discarding message", slog.String("reason", appErr.Message))
			return
		}
		logger.Error("failed to insert email queue", slog.Any("error", err))
		return
	}

	logger.Info("email queued",
		slog.String("emailType", string(msg.EmailType)),
		slog.String("userId", msg.UserId),
	)
}

func main() {
	cfg := config.LoadJSONConfig()

	slogHandler, shutdown, err := telemetry.Setup(context.Background(), cfg.Application.Version)
	if err != nil {
		slog.Error("failed to set up telemetry", slog.Any("error", err))
		os.Exit(1)
	}
	defer shutdown(context.Background())

	logger := slog.New(slogHandler)
	slog.SetDefault(logger)

	db, err := shareddb.OpenPostgres(cfg.ConnectionStrings.PGEmail)
	if err != nil {
		logger.Error("failed to connect to database", slog.Any("error", err))
		os.Exit(1)
	}
	defer db.Close()

	consumer, err := ConnectAmqp[EmailMessage](cfg.RabbitMq)
	if err != nil {
		logger.Error("failed to connect to rabbitmq", slog.Any("error", err))
		os.Exit(1)
	}
	defer consumer.Close()

	deliveries, err := consumer.Consume()
	if err != nil {
		logger.Error("failed to start consuming", slog.Any("error", err))
		os.Exit(1)
	}

	handler := insertemailqueue.NewHandler(emaildb.New(db))
	logger.Info("consumer started", slog.String("queue", "messages.EmailMessage"))

	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	for {
		select {
		case d, ok := <-deliveries:
			if !ok {
				logger.Info("deliveries channel closed")
				return
			}
			handleDelivery(ctx, d, handler, logger)
		case <-ctx.Done():
			logger.Info("consumer shutting down")
			return
		}
	}
}
