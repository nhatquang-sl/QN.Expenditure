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
	getprofile "auth/internal/application/get_profile"
	"auth/internal/application/login"
	"auth/internal/application/logout"
	refreshtoken "auth/internal/application/refresh_token"
	"auth/internal/application/register"
	. "auth/internal/application/shared"
	dbsqlc "auth/internal/database/generated"
	. "auth/internal/services/redis"
)

type AuthController struct {
	login        application.Handler[login.Command, login.Result]
	register     application.Handler[register.Command, register.Result]
	refreshToken application.Handler[refreshtoken.Command, refreshtoken.Result]
	logout       application.Handler[logout.Command, logout.Result]
	getProfile   application.Handler[getprofile.Query, getprofile.Result]
	isDev        bool
}

func NewAuthController(mux *http.ServeMux, db *dbsqlc.Queries, redisService *RedisService, jwtService JwtService, emailService EmailService, logger *slog.Logger, tokenSecret, baseURL string, isDev bool) {
	c := &AuthController{
		login:        login.NewHandler(db, jwtService, logger),
		register:     register.NewHandler(db, emailService, logger, tokenSecret, baseURL),
		refreshToken: refreshtoken.NewHandler(db, jwtService, logger),
		logout:       logout.NewHandler(db),
		getProfile:   getprofile.NewHandler(db, redisService),
		isDev:        isDev,
	}
	auth := middleware.Auth(jwtService)
	mux.HandleFunc("POST /login", c.handleLogin)
	mux.HandleFunc("POST /register", c.handleRegister)
	mux.HandleFunc("POST /refresh-token", c.handleRefreshToken)
	mux.HandleFunc("POST /logout", auth(c.handleLogout))
	mux.HandleFunc("GET /profile", auth(c.handleGetProfile))
}

func (c *AuthController) handleRegister(w http.ResponseWriter, r *http.Request) {
	var cmd register.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		respond.NewResponse(w).JSON(http.StatusBadRequest, nil, apperror.NewBadRequest("invalid request body"))
		return
	}
	result, err := c.register.Handle(r.Context(), cmd)
	respond.NewResponse(w).JSON(http.StatusCreated, result, err)
}

func (c *AuthController) handleLogin(w http.ResponseWriter, r *http.Request) {
	var cmd login.Command
	if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
		respond.NewResponse(w).JSON(http.StatusBadRequest, nil, apperror.NewBadRequest("invalid request body"))
		return
	}
	cmd.IPAddress = clientIP(r)
	cmd.UserAgent = r.Header.Get("User-Agent")

	result, err := c.login.Handle(r.Context(), cmd)
	if err == nil {
		c.setTokenCookies(w, result.AccessToken, result.RefreshToken, result.AccessTokenExpires, result.RefreshTokenExpires)
	}

	respond.NewResponse(w).JSON(http.StatusOK, result, err)
}

func (c *AuthController) handleRefreshToken(w http.ResponseWriter, r *http.Request) {
	cookie, err := r.Cookie("refreshToken")
	if err != nil {
		respond.NewResponse(w).JSON(http.StatusUnauthorized, nil, apperror.NewUnauthorized("missing refresh token"))
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
	respond.NewResponse(w).JSON(http.StatusOK, result, appErr)
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
	claims := middleware.UserFromContext(r.Context())
	_, err := c.logout.Handle(r.Context(), logout.Command{TokenId: claims.TokenId})
	if err == nil {
		c.clearTokenCookies(w)
	}
	respond.NewResponse(w).JSON(http.StatusNoContent, nil, err)
}

func (c *AuthController) handleGetProfile(w http.ResponseWriter, r *http.Request) {
	claims := middleware.UserFromContext(r.Context())
	result, err := c.getProfile.Handle(r.Context(), getprofile.Query{UserId: claims.Id})
	respond.NewResponse(w).JSON(http.StatusOK, result, err)
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

func clientIP(r *http.Request) string {
	if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
		return strings.SplitN(xff, ",", 2)[0]
	}
	ip, _, _ := net.SplitHostPort(r.RemoteAddr)
	return ip
}
