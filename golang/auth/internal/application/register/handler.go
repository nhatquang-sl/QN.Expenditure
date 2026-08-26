package register

import (
	"context"
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"encoding/base64"
	"encoding/binary"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"auth/internal/application"
	"auth/internal/application/apperror"
	"auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"

	"github.com/google/uuid"
	"golang.org/x/crypto/pbkdf2"
)

type Command struct {
	Email     string `json:"email"     validate:"required,email"`
	Password  string `json:"password"  validate:"required,password_strength"`
	FirstName string `json:"firstName" validate:"required"`
	LastName  string `json:"lastName"  validate:"required"`
}

type Result struct {
	Id        string `json:"id"`
	Email     string `json:"email"`
	FirstName string `json:"firstName"`
	LastName  string `json:"lastName"`
}

type handler struct {
	db           *dbsqlc.Queries
	emailService shared.EmailService
	logger       *slog.Logger
	tokenSecret  []byte
	baseURL      string
}

func NewHandler(db *dbsqlc.Queries, emailService shared.EmailService, logger *slog.Logger, tokenSecret, baseURL string) application.Handler[Command, Result] {
	return newValidator(handler{
		db:          db,
		emailService: emailService,
		logger:      logger,
		tokenSecret: []byte(tokenSecret),
		baseURL:     baseURL,
	})
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	normalizedEmail := strings.ToUpper(cmd.Email)

	exists, err := h.db.UserExistsByNormalizedEmail(ctx, normalizedEmail)
	if err != nil {
		return Result{}, err
	}
	if exists {
		return Result{}, apperror.NewConflict("email already registered")
	}

	hash, err := hashPassword(cmd.Password)
	if err != nil {
		return Result{}, err
	}

	id := uuid.New().String()
	if err := h.db.CreateUser(ctx, dbsqlc.CreateUserParams{
		Id:                 id,
		UserName:           cmd.Email,
		NormalizedUserName: normalizedEmail,
		Email:              cmd.Email,
		NormalizedEmail:    normalizedEmail,
		EmailConfirmed:     false,
		PasswordHash:       hash,
		SecurityStamp:      uuid.New().String(),
		ConcurrencyStamp:   uuid.New().String(),
		FirstName:          cmd.FirstName,
		LastName:           cmd.LastName,
	}); err != nil {
		return Result{}, err
	}

	token := generateConfirmToken(id, h.tokenSecret)
	confirmURL := fmt.Sprintf("%s/api/auth/confirm-email?token=%s", h.baseURL, token)

	if h.emailService != nil {
		go func() {
			sendCtx := context.WithoutCancel(ctx)
			if err := h.emailService.SendEmailConfirmation(sendCtx, cmd.Email, cmd.FirstName, confirmURL); err != nil {
				h.logger.ErrorContext(sendCtx, "failed to send confirmation email",
					slog.String("userId", id),
					slog.Any("error", err),
				)
			}
		}()
	}

	return Result{
		Id:        id,
		Email:     cmd.Email,
		FirstName: cmd.FirstName,
		LastName:  cmd.LastName,
	}, nil
}

// hashPassword produces an ASP.NET Identity V3 PBKDF2-HMAC-SHA256 hash.
//
// Wire format (61 bytes):
//
//	byte 0      : 0x01 — format version marker (V3)
//	bytes  1–4  : PRF identifier, big-endian uint32 (1 = PBKDF2-SHA256)
//	bytes  5–8  : iteration count, big-endian uint32 (10 000)
//	bytes  9–12 : salt length in bytes, big-endian uint32 (16)
//	bytes 13–28 : salt  (16 bytes)
//	bytes 29–60 : derived key (32 bytes)
func hashPassword(password string) (string, error) {
	const iterCount = 10000
	const keyLen = 32
	salt := make([]byte, 16)
	if _, err := rand.Read(salt); err != nil {
		return "", err
	}
	derived := pbkdf2.Key([]byte(password), salt, iterCount, keyLen, sha256.New)

	buf := make([]byte, 1+4+4+4+len(salt)+keyLen)
	buf[0] = 0x01
	binary.BigEndian.PutUint32(buf[1:5], 1)
	binary.BigEndian.PutUint32(buf[5:9], uint32(iterCount))
	binary.BigEndian.PutUint32(buf[9:13], uint32(len(salt)))
	copy(buf[13:], salt)
	copy(buf[13+len(salt):], derived)

	return base64.StdEncoding.EncodeToString(buf), nil
}

// generateConfirmToken creates a stateless HMAC-SHA256 email confirmation token.
// Format: base64url(payload) + "." + base64url(HMAC-SHA256(payload, secret))
// Payload: "<userID>:<expiry_unix>" with a 24-hour expiry.
func generateConfirmToken(userID string, secret []byte) string {
	expiry := time.Now().UTC().Add(24 * time.Hour).Unix()
	payload := fmt.Sprintf("%s:%d", userID, expiry)
	payloadEnc := base64.RawURLEncoding.EncodeToString([]byte(payload))

	mac := hmac.New(sha256.New, secret)
	mac.Write([]byte(payloadEnc))
	sig := base64.RawURLEncoding.EncodeToString(mac.Sum(nil))

	return payloadEnc + "." + sig
}
