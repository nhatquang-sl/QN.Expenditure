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
	"reflect"
	"strings"
	"time"

	"auth/internal/application/apperror"
	"auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"

	"github.com/go-playground/validator/v10"
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
	Id                  string
	Email               string
	FirstName           string
	LastName            string
	EmailConfirmed      bool
	AccessToken         string
	RefreshToken        string
	AccessTokenExpires  time.Time
	RefreshTokenExpires time.Time
}

type Handler struct {
	db         *dbsqlc.Queries
	jwtService shared.JwtService
	logger     *slog.Logger
	validate   *validator.Validate
}

func NewHandler(db *dbsqlc.Queries, jwtService shared.JwtService, logger *slog.Logger) *Handler {
	v := validator.New()
	v.RegisterTagNameFunc(func(fld reflect.StructField) string {
		name := strings.SplitN(fld.Tag.Get("json"), ",", 2)[0]
		if name == "-" {
			return ""
		}
		return name
	})
	return &Handler{db: db, jwtService: jwtService, logger: logger, validate: v}
}

func (h *Handler) Handle(ctx context.Context, cmd Command) Result {
	if err := h.validate.Struct(cmd); err != nil {
		var ve validator.ValidationErrors
		if errors.As(err, &ve) {
			panic(apperror.NewValidationErrors(ve))
		}
		panic(err)
	}

	user, err := h.db.GetUserByNormalizedEmail(ctx, strings.ToUpper(cmd.Email))
	if err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			panic(apperror.NewUnauthorized("invalid credentials"))
		}
		panic(err)
	}

	if !verifyPassword(cmd.Password, user.PasswordHash) {
		panic(apperror.NewUnauthorized("invalid credentials"))
	}

	tokens, err := h.jwtService.GenerateTokens(shared.UserClaims{
		Id:             user.Id,
		Email:          user.Email,
		FirstName:      user.FirstName,
		LastName:       user.LastName,
		EmailConfirmed: user.EmailConfirmed,
	}, cmd.RememberMe)
	if err != nil {
		panic(err)
	}

	if err := h.db.CreateLoginHistory(ctx, dbsqlc.CreateLoginHistoryParams{
		UserId:       user.Id,
		IpAddress:    cmd.IPAddress,
		UserAgent:    cmd.UserAgent,
		AccessToken:  tokens.AccessToken,
		RefreshToken: tokens.RefreshToken,
		CreatedAt:    time.Now().UTC(),
		RememberMe:   cmd.RememberMe,
	}); err != nil {
		h.logger.ErrorContext(ctx, "failed to record login history", slog.Any("error", err))
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
	}
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
