package controllertests

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestRefreshToken(t *testing.T) {
	t.Run("Success", refreshTokenSuccess)
	t.Run("NoCookie", refreshTokenNoCookie)
	t.Run("InvalidToken", refreshTokenInvalidToken)
}

func refreshTokenSuccess(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("refresh.success+%d@example.com", time.Now().UnixNano())
	handler := newTestHandler()
	accessToken, refreshToken := loginUserTokens(t, handler, email, "Password1")
	_ = accessToken

	req := httptest.NewRequest(http.MethodPost, "/refresh-token", nil)
	req.AddCookie(&http.Cookie{Name: "refreshToken", Value: refreshToken})
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)

	require.Equal(t, http.StatusOK, w.Code)

	cookies := w.Result().Cookies()
	cookieMap := make(map[string]string)
	for _, c := range cookies {
		cookieMap[c.Name] = c.Value
	}
	assert.NotEmpty(t, cookieMap["accessToken"], "new accessToken cookie should be set")
	assert.NotEmpty(t, cookieMap["refreshToken"], "new refreshToken cookie should be set")

	var result map[string]any
	require.NoError(t, json.NewDecoder(w.Body).Decode(&result))
	assert.Equal(t, email, result["email"])
	assert.NotEmpty(t, result["id"])
}

func refreshTokenNoCookie(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/refresh-token", nil)
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"missing refresh token"}`, w.Body.String())
}

func refreshTokenInvalidToken(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/refresh-token", nil)
	req.AddCookie(&http.Cookie{Name: "refreshToken", Value: "invalid.token.value"})
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"invalid refresh token"}`, w.Body.String())
}

// loginUserTokens registers and logs in, returning both token cookie values.
func loginUserTokens(t *testing.T, handler http.Handler, email, password string) (accessToken, refreshToken string) {
	t.Helper()
	registerUser(t, handler, email, password)

	body, _ := json.Marshal(map[string]string{"email": email, "password": password})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)
	require.Equal(t, http.StatusOK, w.Code)

	for _, c := range w.Result().Cookies() {
		switch c.Name {
		case "accessToken":
			accessToken = c.Value
		case "refreshToken":
			refreshToken = c.Value
		}
	}
	require.NotEmpty(t, accessToken, "accessToken cookie missing from login response")
	require.NotEmpty(t, refreshToken, "refreshToken cookie missing from login response")
	return
}
