package get_profile

import (
	"context"
	"database/sql"
	"errors"

	"auth/internal/application/apperror"
	dbsqlc "auth/internal/database/generated"
)

type Query struct {
	UserId string
}

type Result struct {
	Id             string
	Email          string
	FirstName      string
	LastName       string
	EmailConfirmed bool
}

type Handler struct {
	db *dbsqlc.Queries
}

func NewHandler(db *dbsqlc.Queries) *Handler {
	return &Handler{db: db}
}

func (h *Handler) Handle(ctx context.Context, q Query) Result {
	user, err := h.db.GetUserProfileById(ctx, q.UserId)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			panic(apperror.NewNotFound("user not found"))
		}
		panic(err)
	}

	return Result{
		Id:             user.Id,
		Email:          user.Email,
		FirstName:      user.FirstName,
		LastName:       user.LastName,
		EmailConfirmed: user.EmailConfirmed,
	}
}
