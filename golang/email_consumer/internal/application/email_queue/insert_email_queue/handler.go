package insertemailqueue

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"

	emaildb "qn.expenditure/emaildb/generated"

	. "qn.expenditure/shared/app"
	. "qn.expenditure/shared/apperror"
	"qn.expenditure/shared/messaging/messages"
)

type Command struct {
	UserId      string `validate:"required"`
	ToEmail     string `validate:"required,email"`
	EmailTypeId string `validate:"required"`
	HtmlData    string `validate:"required"`
}

func (c *Command) FromEmailMessage(msg messages.EmailMessage) error {
	data, err := json.Marshal(msg.Data)
	if err != nil {
		return fmt.Errorf("marshal email data: %w", err)
	}
	*c = Command{
		UserId:      msg.UserId,
		ToEmail:     msg.ToEmail,
		EmailTypeId: string(msg.EmailType),
		HtmlData:    string(data),
	}
	return nil
}

type Result struct {
	Id int64
}

type handler struct {
	db *emaildb.Queries
}

func NewHandler(db *emaildb.Queries) Handler[Command, Result] {
	return NewValidator(&handler{db: db})
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	_, err := h.db.GetEmailTypeById(ctx, cmd.EmailTypeId)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return Result{}, NewNotFound("unknown email type: " + cmd.EmailTypeId)
		}
		return Result{}, err
	}

	row, err := h.db.InsertEmailQueue(ctx, emaildb.InsertEmailQueueParams{
		EmailTypeId: cmd.EmailTypeId,
		HtmlData:    cmd.HtmlData,
		UserId:      cmd.UserId,
		ToEmail:     cmd.ToEmail,
	})
	if err != nil {
		return Result{}, err
	}

	return Result{Id: row.Id}, nil
}
