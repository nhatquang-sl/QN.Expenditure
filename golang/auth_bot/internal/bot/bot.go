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

type loginRequest struct {
	Email    string `json:"email"`
	Password string `json:"password"`
}

func (b *Bot) Login(ctx context.Context) {
	b.mu.Lock()
	if len(b.users) == 0 {
		b.mu.Unlock()
		b.logger.WarnContext(ctx, "login: no registered users, skipping")
		return
	}
	user := b.users[b.idx%len(b.users)]
	b.idx++
	b.mu.Unlock()

	// Step 1: POST /login
	body, _ := json.Marshal(loginRequest{Email: user.email, Password: user.password})
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, b.baseURL+"/login", bytes.NewReader(body))
	if err != nil {
		b.logger.ErrorContext(ctx, "login: failed to build request", slog.Any("error", err))
		return
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "login: request failed", slog.Any("error", err))
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		b.logger.WarnContext(ctx, "login: unexpected status",
			slog.Int("status", resp.StatusCode),
			slog.String("email", user.email),
		)
		return
	}

	var accessToken, refreshToken string
	for _, c := range resp.Cookies() {
		switch c.Name {
		case "accessToken":
			accessToken = c.Value
		case "refreshToken":
			refreshToken = c.Value
		}
	}
	if accessToken == "" || refreshToken == "" {
		b.logger.WarnContext(ctx, "login: missing tokens in response", slog.String("email", user.email))
		return
	}

	// Step 2: POST /refresh-token
	req, err = http.NewRequestWithContext(ctx, http.MethodPost, b.baseURL+"/refresh-token", nil)
	if err != nil {
		b.logger.ErrorContext(ctx, "refresh: failed to build request", slog.Any("error", err))
		return
	}
	req.AddCookie(&http.Cookie{Name: "refreshToken", Value: refreshToken})

	resp2, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "refresh: request failed", slog.Any("error", err))
		return
	}
	defer resp2.Body.Close()

	if resp2.StatusCode != http.StatusOK {
		b.logger.WarnContext(ctx, "refresh: unexpected status",
			slog.Int("status", resp2.StatusCode),
			slog.String("email", user.email),
		)
		return
	}

	for _, c := range resp2.Cookies() {
		switch c.Name {
		case "accessToken":
			accessToken = c.Value
		case "refreshToken":
			refreshToken = c.Value
		}
	}

	// Step 3: GET /profile
	req, err = http.NewRequestWithContext(ctx, http.MethodGet, b.baseURL+"/profile", nil)
	if err != nil {
		b.logger.ErrorContext(ctx, "profile: failed to build request", slog.Any("error", err))
		return
	}
	req.AddCookie(&http.Cookie{Name: "accessToken", Value: accessToken})

	resp3, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "profile: request failed", slog.Any("error", err))
		return
	}
	defer resp3.Body.Close()

	if resp3.StatusCode != http.StatusOK {
		b.logger.WarnContext(ctx, "profile: unexpected status",
			slog.Int("status", resp3.StatusCode),
			slog.String("email", user.email),
		)
		return
	}

	b.logger.InfoContext(ctx, "login: cycle complete", slog.String("email", user.email))
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
