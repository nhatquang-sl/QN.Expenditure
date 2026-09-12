package getemailqueueactive

import (
	"context"
	"database/sql"

	emaildb "qn.expenditure/emaildb/generated"

	. "qn.expenditure/shared/app"
)

type Command struct {
	BatchSize int32 `validate:"required,min=1"`
}

type Result struct {
	Items []emaildb.EmailQueue
}

type handler struct {
	db *sql.DB
}

func NewHandler(db *sql.DB) Handler[Command, Result] {
	return NewValidator(&handler{db: db})
}

// Handle claims a batch of eligible EmailQueue rows atomically.
// It selects rows using FOR UPDATE SKIP LOCKED, updates their status to 'sending',
// and commits — all in a single transaction. Returned rows are already marked 'sending'.
func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	tx, err := h.db.BeginTx(ctx, nil)
	if err != nil {
		return Result{}, err
	}
	defer tx.Rollback()

	q := emaildb.New(tx)

	items, err := q.GetEligibleEmailQueueBatch(ctx, cmd.BatchSize)
	if err != nil {
		return Result{}, err
	}

	for _, item := range items {
		if err := q.UpdateEmailQueueSending(ctx, item.Id); err != nil {
			return Result{}, err
		}
	}

	if err := tx.Commit(); err != nil {
		return Result{}, err
	}

	return Result{Items: items}, nil
}
