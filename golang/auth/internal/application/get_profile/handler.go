package get_profile

import (
	"context"
	"database/sql"
	"errors"

	"auth/internal/application"
	"auth/internal/application/apperror"
	dbsqlc "auth/internal/database/generated"
)

type Query struct {
	UserId string
}

type Result struct {
	Id             string `json:"id"`
	Email          string `json:"email"`
	FirstName      string `json:"firstName"`
	LastName       string `json:"lastName"`
	EmailConfirmed bool   `json:"emailConfirmed"`
}

type handler struct {
	db *dbsqlc.Queries
}

func NewHandler(db *dbsqlc.Queries) application.Handler[Query, Result] {
	return &handler{db: db}
}

func (h *handler) Handle(ctx context.Context, q Query) (Result, error) {
	user, err := h.db.GetUserProfileById(ctx, q.UserId)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return Result{}, apperror.NewNotFound("user not found")
		}
		return Result{}, err
	}

	return Result{
		Id:             user.Id,
		Email:          user.Email,
		FirstName:      user.FirstName,
		LastName:       user.LastName,
		EmailConfirmed: user.EmailConfirmed,
	}, nil
}
