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
	confirmemail "auth/internal/application/confirm_email"
	getprofile "auth/internal/application/get_profile"
	"auth/internal/application/login"
	"auth/internal/application/logout"
	refreshtoken "auth/internal/application/refresh_token"
	"auth/internal/application/register"
	. "auth/internal/application/shared"
	. "auth/internal/config"
	dbsqlc "auth/internal/database/generated"
	. "auth/internal/services/redis"

	. "qn.expenditure/shared/app"
	. "qn.expenditure/shared/apperror"
)

type AuthController struct {
	login        Handler[login.Command, login.Result]
	register     Handler[register.Command, register.Result]
	confirmEmail Handler[confirmemail.Command, confirmemail.Result]
	refreshToken Handler[refreshtoken.Command, refreshtoken.Result]
	logout       Handler[logout.Command, logout.Result]
	getProfile   Handler[getprofile.Query, getprofile.Result]
	logger       *slog.Logger
	isDev        bool
}

func NewAuthController(mux *http.ServeMux, cfg *Config, db *dbsqlc.Queries, redisService *RedisService, jwtService JwtService, logger *slog.Logger, tokenSecret string, isDev bool) {
	c := &AuthController{
		login:        login.NewHandler(db, jwtService, logger),
		register:     register.NewHandler(db, logger, &cfg.RabbitMq, tokenSecret, cfg.Application.Endpoint),
		confirmEmail: confirmemail.NewHandler(db, tokenSecret),
		refreshToken: refreshtoken.NewHandler(db, jwtService, logger),
		logout:       logout.NewHandler(db, jwtService, redisService),
		getProfile:   getprofile.NewHandler(db, redisService, logger),
		logger:       logger,
		isDev:        isDev,
	}
	auth := middleware.Auth(jwtService, redisService, logger)
	mux.HandleFunc("POST /login", c.handleLogin)
	mux.HandleFunc("POST /register", c.handleRegister)
	mux.HandleFunc("GET /confirm-email", c.handleConfirmEmail)
	mux.HandleFunc("POST /refresh-token", c.handleRefreshToken)
	mux.HandleFunc("POST /logout", auth(c.handleLogout))
	mux.HandleFunc("GET /profile", auth(c.handleGetProfile))
}

func (c *AuthController) handleRegister(w http.ResponseWriter, r *http.Request) {
	var cmd register.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		respond.NewResponse(w, c.logger).JSON(http.StatusBadRequest, nil, NewBadRequest("invalid request body"))
		return
	}
	result, err := c.register.Handle(r.Context(), cmd)
	respond.NewResponse(w, c.logger).JSON(http.StatusCreated, result, err)
}

func (c *AuthController) handleLogin(w http.ResponseWriter, r *http.Request) {
	var cmd login.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		respond.NewResponse(w, c.logger).JSON(http.StatusBadRequest, nil, NewBadRequest("invalid request body"))
		return
	}
	cmd.IPAddress = clientIP(r)
	cmd.UserAgent = r.Header.Get("User-Agent")

	result, err := c.login.Handle(r.Context(), cmd)
	if err == nil {
		c.setTokenCookies(w, result.AccessToken, result.RefreshToken, result.AccessTokenExpires, result.RefreshTokenExpires)
	}

	respond.NewResponse(w, c.logger).JSON(http.StatusOK, result, err)
}

func (c *AuthController) handleRefreshToken(w http.ResponseWriter, r *http.Request) {
	cookie, err := r.Cookie("refreshToken")
	if err != nil {
		respond.NewResponse(w, c.logger).JSON(http.StatusUnauthorized, nil, NewUnauthorized("missing refresh token"))
		return
	}
	result, appErr := c.refreshToken.Handle(r.Context(), refreshtoken.Command{
		RefreshToken: cookie.Value,
		IPAddress:    clientIP(r),
		UserAgent:    r.Header.Get("User-Agent"),
	})
	if appErr == nil {
		c.setTokenCookies(w, result.AccessToken, result.RefreshToken, result.AccessTokenExpires, result.RefreshTokenExpires)
	}
	respond.NewResponse(w, c.logger).JSON(http.StatusOK, result, appErr)
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

func (c *AuthController) handleLogout(w http.ResponseWriter, r *http.Request) {
	refreshCookie, err := r.Cookie("refreshToken")
	if err != nil {
		respond.NewResponse(w, c.logger).JSON(http.StatusUnauthorized, nil, NewUnauthorized("missing refresh token"))
		return
	}
	_, err = c.logout.Handle(r.Context(), logout.Command{
		RefreshToken: refreshCookie.Value,
	})
	if err == nil {
		c.clearTokenCookies(w)
	}
	respond.NewResponse(w, c.logger).JSON(http.StatusNoContent, nil, err)
}

func (c *AuthController) handleGetProfile(w http.ResponseWriter, r *http.Request) {
	claims := middleware.UserFromContext(r.Context())
	result, err := c.getProfile.Handle(r.Context(), getprofile.Query{UserId: claims.Id})
	respond.NewResponse(w, c.logger).JSON(http.StatusOK, result, err)
}

func (c *AuthController) clearTokenCookies(w http.ResponseWriter) {
	secure := !c.isDev
	sameSite := http.SameSiteStrictMode
	for _, name := range []string{"accessToken", "refreshToken"} {
		http.SetCookie(w, &http.Cookie{
			Name:     name,
			Value:    "",
			Path:     "/",
			MaxAge:   -1,
			HttpOnly: true,
			Secure:   secure,
			SameSite: sameSite,
		})
	}
}

func (c *AuthController) handleConfirmEmail(w http.ResponseWriter, r *http.Request) {
	token := r.URL.Query().Get("token")
	if token == "" {
		respond.NewResponse(w, c.logger).JSON(http.StatusBadRequest, nil, NewBadRequest("missing token"))
		return
	}
	_, err := c.confirmEmail.Handle(r.Context(), confirmemail.Command{Token: token})
	respond.NewResponse(w, c.logger).JSON(http.StatusOK, nil, err)
}

func clientIP(r *http.Request) string {
	if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
		return strings.SplitN(xff, ",", 2)[0]
	}
	ip, _, _ := net.SplitHostPort(r.RemoteAddr)
	return ip
}
