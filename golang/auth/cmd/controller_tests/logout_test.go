package controllertests

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestLogout(t *testing.T) {
	t.Run("Success", logoutSuccess)
	t.Run("NoCookie", logoutNoCookie)
	t.Run("InvalidToken", logoutInvalidToken)
}

func logoutSuccess(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("logout.success+%d@example.com", time.Now().UnixNano())
	handler := newTestHandler()
	accessToken := loginUser(t, handler, email, "Password1")

	// Capture the tokenId from the access token before logout
	claims, err := testJwtService.ValidateAccessToken(accessToken)
	require.NoError(t, err)
	require.NotNil(t, claims)

	req := httptest.NewRequest(http.MethodPost, "/logout", nil)
	req.AddCookie(&http.Cookie{Name: "accessToken", Value: accessToken})
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)

	require.Equal(t, http.StatusNoContent, w.Code)

	// Cookies should be cleared (MaxAge = -1)
	cookieMap := make(map[string]*http.Cookie)
	for _, c := range w.Result().Cookies() {
		cookieMap[c.Name] = c
	}
	assert.Equal(t, -1, cookieMap["accessToken"].MaxAge)
	assert.Equal(t, -1, cookieMap["refreshToken"].MaxAge)

	// Login history row should be deleted
	_, err = testQueries.GetLoginHistoryById(context.Background(), claims.TokenId)
	assert.True(t, errors.Is(err, sql.ErrNoRows), "login history should be deleted after logout")

	// Redis session entry should be removed
	exists, err := testCache.Exists(context.Background(), fmt.Sprintf("session:%d", claims.TokenId))
	require.NoError(t, err)
	assert.False(t, exists, "redis session entry should be deleted after logout")

	// The old access token should now be rejected (session invalidated)
	req2 := httptest.NewRequest(http.MethodGet, "/profile", nil)
	req2.AddCookie(&http.Cookie{Name: "accessToken", Value: accessToken})
	w2 := httptest.NewRecorder()
	handler.ServeHTTP(w2, req2)
	require.Equal(t, http.StatusUnauthorized, w2.Code)
	assert.JSONEq(t, `{"message":"session invalidated"}`, w2.Body.String())
}

func logoutNoCookie(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/logout", nil)
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"missing access token"}`, w.Body.String())
}

func logoutInvalidToken(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/logout", nil)
	req.AddCookie(&http.Cookie{Name: "accessToken", Value: "invalid.token.value"})
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"invalid access token"}`, w.Body.String())
}
