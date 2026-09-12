package worker

import (
	"bytes"
	"context"
	"database/sql"
	"encoding/json"
	"html/template"
	"log/slog"
	"math"
	"time"

	getemailqueueactive "email_worker/internal/application/email_queue/get_email_queue_active"

	emaildb "qn.expenditure/emaildb/generated"
)

// EmailSender sends a single transactional email.
type EmailSender interface {
	Send(ctx context.Context, to, subject, htmlBody string) error
}

type Worker struct {
	db      *sql.DB
	queries *emaildb.Queries
	sender  EmailSender
	logger  *slog.Logger
}

func New(db *sql.DB, queries *emaildb.Queries, sender EmailSender, logger *slog.Logger) *Worker {
	return &Worker{db: db, queries: queries, sender: sender, logger: logger}
}

// Tick claims one batch of eligible EmailQueue rows and processes each one.
func (w *Worker) Tick(ctx context.Context, batchSize int32) {
	result, err := getemailqueueactive.NewHandler(w.db).Handle(ctx, getemailqueueactive.Command{BatchSize: batchSize})
	if err != nil {
		w.logger.ErrorContext(ctx, "failed to claim email batch", slog.Any("error", err))
		return
	}

	for _, item := range result.Items {
		w.process(ctx, item)
	}
}

func (w *Worker) process(ctx context.Context, item emaildb.EmailQueue) {
	emailType, err := w.queries.GetEmailTypeById(ctx, item.EmailTypeId)
	if err != nil {
		w.logger.ErrorContext(ctx, "failed to fetch email type",
			slog.Int64("id", item.Id), slog.Any("error", err))
		w.fail(ctx, item)
		return
	}

	var data map[string]any
	if err := json.Unmarshal([]byte(item.HtmlData), &data); err != nil {
		w.logger.ErrorContext(ctx, "failed to unmarshal html data",
			slog.Int64("id", item.Id), slog.Any("error", err))
		w.fail(ctx, item)
		return
	}

	tmpl, err := template.New("email").Parse(emailType.HtmlTemplate)
	if err != nil {
		w.logger.ErrorContext(ctx, "failed to parse email template",
			slog.Int64("id", item.Id), slog.Any("error", err))
		w.fail(ctx, item)
		return
	}

	var buf bytes.Buffer
	if err := tmpl.Execute(&buf, data); err != nil {
		w.logger.ErrorContext(ctx, "failed to render email template",
			slog.Int64("id", item.Id), slog.Any("error", err))
		w.fail(ctx, item)
		return
	}

	if err := w.sender.Send(ctx, item.ToEmail, emailType.Subject, buf.String()); err != nil {
		w.logger.ErrorContext(ctx, "failed to send email",
			slog.Int64("id", item.Id), slog.Any("error", err))
		w.fail(ctx, item)
		return
	}

	if err := w.queries.UpdateEmailQueueSent(ctx, item.Id); err != nil {
		w.logger.ErrorContext(ctx, "failed to mark email sent",
			slog.Int64("id", item.Id), slog.Any("error", err))
		return
	}

	w.logger.InfoContext(ctx, "email sent",
		slog.Int64("id", item.Id),
		slog.String("emailType", item.EmailTypeId),
		slog.String("to", item.ToEmail),
	)
}

func (w *Worker) fail(ctx context.Context, item emaildb.EmailQueue) {
	newRetry := item.Retry + 1

	var nextRetryAt sql.NullTime
	if newRetry < 3 {
		delay := time.Duration(math.Pow(2, float64(newRetry))) * time.Minute
		nextRetryAt = sql.NullTime{Time: time.Now().Add(delay), Valid: true}
	}

	if err := w.queries.UpdateEmailQueueFailed(ctx, emaildb.UpdateEmailQueueFailedParams{
		Id:          item.Id,
		Retry:       newRetry,
		NextRetryAt: nextRetryAt,
	}); err != nil {
		w.logger.ErrorContext(ctx, "failed to mark email failed",
			slog.Int64("id", item.Id), slog.Any("error", err))
	}
}
