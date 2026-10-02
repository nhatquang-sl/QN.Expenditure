package bot

import (
	"bytes"
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"sync"
	"time"
)

type botUser struct {
	email string
}

type Bot struct {
	mu         sync.Mutex
	users      []botUser
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
	users := make([]botUser, len(b.users))
	copy(users, b.users)
	b.mu.Unlock()

	if len(users) == 0 {
		b.logger.WarnContext(ctx, "login: no registered users, skipping")
		return
	}

	var wg sync.WaitGroup
	for i, user := range users {
		wg.Add(1)
		go func(i int, user botUser) {
			defer wg.Done()
			accessToken, refreshToken := b.doLogin(ctx, user)
			if accessToken == "" {
				return
			}

			if i%3 == 0 {
				if newAccess, _ := b.doRefresh(ctx, user, refreshToken); newAccess != "" {
					accessToken = newAccess
				}
			}

			if i%2 == 0 {
				b.doProfile(ctx, user, accessToken)
			}
		}(i, user)
	}
	wg.Wait()
}

func (b *Bot) doLogin(ctx context.Context, user botUser) (accessToken, refreshToken string) {
	body, _ := json.Marshal(loginRequest{Email: user.email, Password: b.password})
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, b.baseURL+"/login", bytes.NewReader(body))
	if err != nil {
		b.logger.ErrorContext(ctx, "login: failed to build request", slog.Any("error", err))
		return "", ""
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "login: request failed", slog.Any("error", err))
		return "", ""
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		b.logger.WarnContext(ctx, "login: unexpected status",
			slog.Int("status", resp.StatusCode),
			slog.String("email", user.email),
		)
		return "", ""
	}

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
		return "", ""
	}

	return accessToken, refreshToken
}

func (b *Bot) doRefresh(ctx context.Context, user botUser, refreshToken string) (accessToken, newRefreshToken string) {
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, b.baseURL+"/refresh-token", nil)
	if err != nil {
		b.logger.ErrorContext(ctx, "refresh: failed to build request", slog.Any("error", err))
		return "", ""
	}
	req.AddCookie(&http.Cookie{Name: "refreshToken", Value: refreshToken})

	resp, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "refresh: request failed", slog.Any("error", err))
		return "", ""
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		b.logger.WarnContext(ctx, "refresh: unexpected status",
			slog.Int("status", resp.StatusCode),
			slog.String("email", user.email),
		)
		return "", ""
	}

	for _, c := range resp.Cookies() {
		switch c.Name {
		case "accessToken":
			accessToken = c.Value
		case "refreshToken":
			newRefreshToken = c.Value
		}
	}

	return accessToken, newRefreshToken
}

func (b *Bot) doProfile(ctx context.Context, user botUser, accessToken string) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, b.baseURL+"/profile", nil)
	if err != nil {
		b.logger.ErrorContext(ctx, "profile: failed to build request", slog.Any("error", err))
		return
	}
	req.AddCookie(&http.Cookie{Name: "accessToken", Value: accessToken})

	resp, err := b.httpClient.Do(req)
	if err != nil {
		b.logger.ErrorContext(ctx, "profile: request failed", slog.Any("error", err))
		return
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		b.logger.WarnContext(ctx, "profile: unexpected status",
			slog.Int("status", resp.StatusCode),
			slog.String("email", user.email),
		)
		return
	}

	b.logger.InfoContext(ctx, "login: cycle complete", slog.String("email", user.email))
}

// Seed pre-populates the user list from the database with existing bot accounts
// (email LIKE 'bot+%@yopmail.com'). Called once on startup so the login loop
// has users available immediately, even after a restart.
func (b *Bot) Seed(ctx context.Context, db *sql.DB) {
	rows, err := db.QueryContext(ctx, `SELECT "Email" FROM "Users" WHERE "Email" LIKE 'bot%@yopmail.com' LIMIT 500`)
	if err != nil {
		b.logger.ErrorContext(ctx, "seed: failed to query bot users", slog.Any("error", err))
		return
	}
	defer rows.Close()

	var count int
	for rows.Next() {
		var email string
		if err := rows.Scan(&email); err != nil {
			b.logger.ErrorContext(ctx, "seed: failed to scan email", slog.Any("error", err))
			continue
		}
		b.mu.Lock()
		b.users = append(b.users, botUser{email: email})
		b.mu.Unlock()
		count++
	}

	if err := rows.Err(); err != nil {
		b.logger.ErrorContext(ctx, "seed: row iteration error", slog.Any("error", err))
	}

	b.logger.InfoContext(ctx, "seed: loaded bot users from db", slog.Int("count", count))
}

type registerRequest struct {
	Email     string `json:"email"`
	Password  string `json:"password"`
	FirstName string `json:"firstName"`
	LastName  string `json:"lastName"`
}

func (b *Bot) Register(ctx context.Context) {
	email := fmt.Sprintf("bot%d@yopmail.com", time.Now().UnixNano())
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
	b.users = append(b.users, botUser{email: email})
	b.mu.Unlock()

	b.logger.InfoContext(ctx, "register: success", slog.String("email", email))
}
