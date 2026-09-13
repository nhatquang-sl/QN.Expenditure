package getprofile

import (
	"context"
	"database/sql"
	"errors"
	"log/slog"

	"auth/internal/application"
	dbsqlc "auth/internal/database/generated"
	. "auth/internal/services/redis"

	. "qn.expenditure/shared/app"
	. "qn.expenditure/shared/apperror"
)

type Query struct {
	UserId string
}

type Result struct {
	Id             string   `json:"id"`
	Email          string   `json:"email"`
	FirstName      string   `json:"firstName"`
	LastName       string   `json:"lastName"`
	EmailConfirmed bool     `json:"emailConfirmed"`
	Roles          []string `json:"roles"`
}

type handler struct {
	db     *dbsqlc.Queries
	logger *slog.Logger
}

func NewHandler(db *dbsqlc.Queries, cache *RedisService, logger *slog.Logger) Handler[Query, Result] {
	return application.NewCacher(
		&handler{db: db, logger: logger},
		cache,
		func(q Query) string { return "profile:" + q.UserId },
	)
}

func (h *handler) Handle(ctx context.Context, q Query) (Result, error) {
	user, err := h.db.GetUserProfileById(ctx, q.UserId)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			h.logger.WarnContext(ctx, "get_profile: user not found", slog.String("userId", q.UserId))
			return Result{}, NewNotFound("user not found")
		}
		h.logger.ErrorContext(ctx, "get_profile: db error", slog.String("userId", q.UserId), slog.Any("error", err))
		return Result{}, err
	}

	roles, err := h.db.GetUserRoles(ctx, q.UserId)
	if err != nil {
		h.logger.ErrorContext(ctx, "get_profile: failed to fetch roles", slog.String("email", user.Email), slog.Any("error", err))
		return Result{}, err
	}

	h.logger.InfoContext(ctx, "get_profile: fetched from db", slog.String("email", user.Email))

	return Result{
		Id:             user.Id,
		Email:          user.Email,
		FirstName:      user.FirstName,
		LastName:       user.LastName,
		EmailConfirmed: user.EmailConfirmed,
		Roles:          roles,
	}, nil
}
