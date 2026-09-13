package confirmemail

import (
	"context"
	"database/sql"
	"errors"

	. "auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"

	. "qn.expenditure/shared/app"
	. "qn.expenditure/shared/apperror"
)

type Command struct {
	Token string
}

type Result struct{}

type handler struct {
	db          *dbsqlc.Queries
	tokenSecret []byte
}

func NewHandler(db *dbsqlc.Queries, tokenSecret string) Handler[Command, Result] {
	return handler{db: db, tokenSecret: []byte(tokenSecret)}
}

func (h handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	userID, err := ParseConfirmToken(cmd.Token, h.tokenSecret)
	if err != nil {
		return Result{}, NewBadRequest("invalid or expired confirmation token")
	}

	user, err := h.db.GetUserProfileById(ctx, userID)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return Result{}, NewNotFound("user not found")
		}
		return Result{}, err
	}

	if user.EmailConfirmed {
		return Result{}, nil
	}

	if err := h.db.ConfirmUserEmail(ctx, userID); err != nil {
		return Result{}, err
	}

	return Result{}, nil
}
