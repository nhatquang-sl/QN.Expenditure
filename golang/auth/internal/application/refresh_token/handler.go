package refreshtoken

import (
	"context"
	"database/sql"
	"errors"
	"log/slog"
	"time"

	"auth/internal/application"
	"auth/internal/application/apperror"
	. "auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"
)

type Command struct {
	RefreshToken string
	IPAddress    string
	UserAgent    string
}

type Result struct {
	Id                  string    `json:"id"`
	Email               string    `json:"email"`
	FirstName           string    `json:"firstName"`
	LastName            string    `json:"lastName"`
	EmailConfirmed      bool      `json:"emailConfirmed"`
	AccessToken         string    `json:"-"`
	RefreshToken        string    `json:"-"`
	AccessTokenExpires  time.Time `json:"-"`
	RefreshTokenExpires time.Time `json:"-"`
}

type handler struct {
	db         *dbsqlc.Queries
	jwtService JwtService
	logger     *slog.Logger
}

func NewHandler(db *dbsqlc.Queries, jwtService JwtService, logger *slog.Logger) application.Handler[Command, Result] {
	return &handler{db: db, jwtService: jwtService, logger: logger}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	claims, err := h.jwtService.ValidateRefreshToken(cmd.RefreshToken)
	if err != nil || claims == nil {
		return Result{}, apperror.NewUnauthorized("invalid refresh token")
	}

	session, err := h.db.GetUserSessionById(ctx, claims.TokenId)
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return Result{}, apperror.NewUnauthorized("invalid refresh token")
		}
		return Result{}, err
	}

	tokens, err := h.jwtService.GenerateTokens(UserClaims{
		Id:             claims.Id,
		Email:          claims.Email,
		FirstName:      claims.FirstName,
		LastName:       claims.LastName,
		EmailConfirmed: claims.EmailConfirmed,
		TokenId:        claims.TokenId,
	}, session.RememberMe)
	if err != nil {
		return Result{}, err
	}

	if err := h.db.UpdateUserSessionTokens(ctx, dbsqlc.UpdateUserSessionTokensParams{
		Id:           session.Id,
		AccessToken:  tokens.AccessToken,
		RefreshToken: tokens.RefreshToken,
	}); err != nil {
		h.logger.ErrorContext(ctx, "failed to update user session tokens", slog.Any("error", err))
	}

	return Result{
		Id:                  claims.Id,
		Email:               claims.Email,
		FirstName:           claims.FirstName,
		LastName:            claims.LastName,
		EmailConfirmed:      claims.EmailConfirmed,
		AccessToken:         tokens.AccessToken,
		RefreshToken:        tokens.RefreshToken,
		AccessTokenExpires:  tokens.AccessTokenExpires,
		RefreshTokenExpires: tokens.RefreshTokenExpires,
	}, nil
}
