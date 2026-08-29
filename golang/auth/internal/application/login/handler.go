package login

import (
	"context"
	"crypto/sha256"
	"crypto/subtle"
	"database/sql"
	"encoding/base64"
	"encoding/binary"
	"errors"
	"log/slog"
	"strings"
	"time"

	"auth/internal/application"
	"auth/internal/application/apperror"
	. "auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"

	"golang.org/x/crypto/pbkdf2"
)

type Command struct {
	Email      string `json:"email"      validate:"required,email"`
	Password   string `json:"password"   validate:"required"`
	RememberMe bool   `json:"rememberMe"`
	IPAddress  string `json:"-"`
	UserAgent  string `json:"-"`
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
	return newValidator(handler{db: db, jwtService: jwtService, logger: logger})
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	user, err := h.db.GetUserByNormalizedEmail(ctx, strings.ToUpper(cmd.Email))
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return Result{}, apperror.NewUnauthorized("invalid credentials")
		}
		return Result{}, err
	}

	if !verifyPassword(cmd.Password, user.PasswordHash) {
		return Result{}, apperror.NewUnauthorized("invalid credentials")
	}

	historyId, err := h.db.CreateLoginHistory(ctx, dbsqlc.CreateLoginHistoryParams{
		UserId:       user.Id,
		IpAddress:    cmd.IPAddress,
		UserAgent:    cmd.UserAgent,
		AccessToken:  "",
		RefreshToken: "",
		CreatedAt:    time.Now().UTC(),
		RememberMe:   cmd.RememberMe,
	})
	if err != nil {
		h.logger.ErrorContext(ctx, "failed to record login history", slog.Any("error", err))
	}

	tokens, err := h.jwtService.GenerateTokens(UserClaims{
		Id:             user.Id,
		Email:          user.Email,
		FirstName:      user.FirstName,
		LastName:       user.LastName,
		EmailConfirmed: user.EmailConfirmed,
		TokenId:        historyId,
	}, cmd.RememberMe)
	if err != nil {
		return Result{}, err
	}

	if err := h.db.UpdateLoginHistoryTokens(ctx, dbsqlc.UpdateLoginHistoryTokensParams{
		Id:           historyId,
		AccessToken:  tokens.AccessToken,
		RefreshToken: tokens.RefreshToken,
	}); err != nil {
		h.logger.ErrorContext(ctx, "failed to update login history tokens", slog.Any("error", err))
	}

	return Result{
		Id:                  user.Id,
		Email:               user.Email,
		FirstName:           user.FirstName,
		LastName:            user.LastName,
		EmailConfirmed:      user.EmailConfirmed,
		AccessToken:         tokens.AccessToken,
		RefreshToken:        tokens.RefreshToken,
		AccessTokenExpires:  tokens.AccessTokenExpires,
		RefreshTokenExpires: tokens.RefreshTokenExpires,
	}, nil
}

// verifyPassword checks a plain-text password against an ASP.NET Identity V3 PBKDF2-HMAC-SHA256 hash.
//
// Wire format (61 bytes total for the standard configuration):
//
//	byte 0      : 0x01 — format version marker (V3)
//	bytes  1–4  : PRF identifier, big-endian uint32 (1 = PBKDF2-SHA256)
//	bytes  5–8  : iteration count, big-endian uint32 (10 000)
//	bytes  9–12 : salt length in bytes, big-endian uint32 (16)
//	bytes 13–28 : salt  (16 bytes)
//	bytes 29–60 : derived key (32 bytes)
func verifyPassword(password, encodedHash string) bool {
	data, err := base64.StdEncoding.DecodeString(encodedHash)
	if err != nil || len(data) < 13 || data[0] != 0x01 {
		return false
	}
	iterCount := int(binary.BigEndian.Uint32(data[5:9]))
	saltLen := int(binary.BigEndian.Uint32(data[9:13]))
	if len(data) < 13+saltLen {
		return false
	}
	salt := data[13 : 13+saltLen]
	storedKey := data[13+saltLen:]
	derived := pbkdf2.Key([]byte(password), salt, iterCount, len(storedKey), sha256.New)
	return subtle.ConstantTimeCompare(derived, storedKey) == 1
}
