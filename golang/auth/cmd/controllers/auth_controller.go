package controllers

import (
	"encoding/json"
	"log/slog"
	"net"
	"net/http"
	"strings"
	"time"

	"auth/cmd/respond"
	"auth/internal/application/apperror"
	"auth/internal/application/login"
	"auth/internal/application/register"
	"auth/internal/services/email"
	"auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"
)

type AuthController struct {
	login    *login.Handler
	register *register.Handler
	isDev    bool
}

func NewAuthController(mux *http.ServeMux, db *dbsqlc.Queries, jwtService shared.JwtService, emailService email.EmailService, logger *slog.Logger, tokenSecret, baseURL string, isDev bool) {
	c := &AuthController{
		login:    login.NewHandler(db, jwtService, logger),
		register: register.NewHandler(db, emailService, logger, tokenSecret, baseURL),
		isDev:    isDev,
	}
	mux.HandleFunc("POST /api/auth/login", c.handleLogin)
	mux.HandleFunc("POST /api/auth/register", c.handleRegister)
}

func (c *AuthController) handleRegister(w http.ResponseWriter, r *http.Request) {
	var cmd register.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		panic(apperror.NewBadRequest("invalid request body"))
	}
	result := c.register.Handle(r.Context(), cmd)
	respond.NewResponse(w).JSON(http.StatusCreated, map[string]any{
		"id":        result.Id,
		"email":     result.Email,
		"firstName": result.FirstName,
		"lastName":  result.LastName,
	})
}

func (c *AuthController) handleLogin(w http.ResponseWriter, r *http.Request) {
	var cmd login.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		panic(apperror.NewBadRequest("invalid request body"))
	}
	cmd.IPAddress = clientIP(r)
	cmd.UserAgent = r.Header.Get("User-Agent")

	result := c.login.Handle(r.Context(), cmd)

	c.setTokenCookies(w, result.AccessToken, result.RefreshToken, result.AccessTokenExpires, result.RefreshTokenExpires)

	respond.NewResponse(w).OK(map[string]any{
		"id":             result.Id,
		"email":          result.Email,
		"firstName":      result.FirstName,
		"lastName":       result.LastName,
		"emailConfirmed": result.EmailConfirmed,
	})
}

func (c *AuthController) setTokenCookies(w http.ResponseWriter, accessToken, refreshToken string, atExp, rtExp time.Time) {
	secure := !c.isDev
	sameSite := http.SameSiteStrictMode

	http.SetCookie(w, &http.Cookie{
		Name:     "accessToken",
		Value:    accessToken,
		Path:     "/",
		MaxAge:   int(time.Until(atExp).Seconds()),
		HttpOnly: true,
		Secure:   secure,
		SameSite: sameSite,
	})
	http.SetCookie(w, &http.Cookie{
		Name:     "refreshToken",
		Value:    refreshToken,
		Path:     "/",
		MaxAge:   int(time.Until(rtExp).Seconds()),
		HttpOnly: true,
		Secure:   secure,
		SameSite: sameSite,
	})
}


func clientIP(r *http.Request) string {
	if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
		return strings.SplitN(xff, ",", 2)[0]
	}
	ip, _, _ := net.SplitHostPort(r.RemoteAddr)
	return ip
}
