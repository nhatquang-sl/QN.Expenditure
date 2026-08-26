package controllers

import (
	"encoding/json"
	"log/slog"
	"net"
	"net/http"
	"strings"
	"time"

	"auth/cmd/middleware"
	"auth/cmd/respond"
	"auth/internal/application"
	"auth/internal/application/apperror"
	"auth/internal/application/get_profile"
	"auth/internal/application/login"
	"auth/internal/application/register"
	"auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"
)

type AuthController struct {
	login      application.Handler[login.Command, login.Result]
	register   application.Handler[register.Command, register.Result]
	getProfile application.Handler[get_profile.Query, get_profile.Result]
	isDev      bool
}

func NewAuthController(mux *http.ServeMux, db *dbsqlc.Queries, jwtService shared.JwtService, emailService shared.EmailService, logger *slog.Logger, tokenSecret, baseURL string, isDev bool) {
	c := &AuthController{
		login:      login.NewHandler(db, jwtService, logger),
		register:   register.NewHandler(db, emailService, logger, tokenSecret, baseURL),
		getProfile: get_profile.NewHandler(db),
		isDev:      isDev,
	}
	auth := middleware.Auth(jwtService)
	mux.HandleFunc("POST /login", c.handleLogin)
	mux.HandleFunc("POST /register", c.handleRegister)
	mux.Handle("GET /profile", auth(http.HandlerFunc(c.handleGetProfile)))
}

func (c *AuthController) handleRegister(w http.ResponseWriter, r *http.Request) {
	var cmd register.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		panic(apperror.NewBadRequest("invalid request body"))
	}
	result, err := c.register.Handle(r.Context(), cmd)
	respond.NewResponse(w).JSON(http.StatusCreated, result, err)
}

func (c *AuthController) handleLogin(w http.ResponseWriter, r *http.Request) {
	var cmd login.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		panic(apperror.NewBadRequest("invalid request body"))
	}
	cmd.IPAddress = clientIP(r)
	cmd.UserAgent = r.Header.Get("User-Agent")

	result, err := c.login.Handle(r.Context(), cmd)
	if err == nil {
		c.setTokenCookies(w, result.AccessToken, result.RefreshToken, result.AccessTokenExpires, result.RefreshTokenExpires)
	}

	respond.NewResponse(w).JSON(http.StatusOK, result, err)
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

func (c *AuthController) handleGetProfile(w http.ResponseWriter, r *http.Request) {
	claims, err := middleware.UserFromContext(r.Context())
	if err != nil {
		respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, err)
		return
	}
	result, err := c.getProfile.Handle(r.Context(), get_profile.Query{UserId: claims.Id})
	respond.NewResponse(w).JSON(http.StatusOK, result, err)
}

func clientIP(r *http.Request) string {
	if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
		return strings.SplitN(xff, ",", 2)[0]
	}
	ip, _, _ := net.SplitHostPort(r.RemoteAddr)
	return ip
}
