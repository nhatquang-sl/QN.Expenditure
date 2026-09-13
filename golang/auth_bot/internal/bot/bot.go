package bot

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"sync"
	"time"
)

type botUser struct {
	email    string
	password string
}

type Bot struct {
	mu         sync.Mutex
	users      []botUser
	idx        int
	baseURL    string
	password   string
	httpClient *http.Client
	logger     *slog.Logger
}

func New(baseURL, password string, client *http.Client, logger *slog.Logger) *Bot {
	return &Bot{
		baseURL:    baseURL,
		password:   password,
		httpClient: client,
		logger:     logger,
	}
}

// UserCount returns the number of successfully registered bot users.
func (b *Bot) UserCount() int {
	b.mu.Lock()
	defer b.mu.Unlock()
	return len(b.users)
}

type registerRequest struct {
	Email     string `json:"email"`
	Password  string `json:"password"`
	FirstName string `json:"firstName"`
	LastName  string `json:"lastName"`
}

func (b *Bot) Register(ctx context.Context) {
	email := fmt.Sprintf("bot+%d@yopmail.com", time.Now().UnixMilli())
	body, _ := json.Marshal(registerRequest{
		Email:     email,
		Password:  b.password,
		FirstName: "Bot",
		LastName:  "User",
	})

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, b.baseURL+"/register", bytes.NewReader(body))
	if err != nil {
		b.logger.ErrorContext(ctx, "register: failed to build request", slog.Any("error", err))
		return
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "register: request failed", slog.Any("error", err))
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusCreated {
		b.logger.WarnContext(ctx, "register: unexpected status",
			slog.Int("status", resp.StatusCode),
			slog.String("email", email),
		)
		return
	}

	b.mu.Lock()
	b.users = append(b.users, botUser{email: email, password: b.password})
	b.mu.Unlock()

	b.logger.InfoContext(ctx, "register: success", slog.String("email", email))
}
