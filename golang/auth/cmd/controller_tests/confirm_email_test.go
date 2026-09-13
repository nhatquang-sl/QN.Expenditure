package controllertests

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"auth/internal/application/register"
	. "auth/internal/application/shared"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestConfirmEmail(t *testing.T) {
	t.Run("Success", confirmEmailSuccess)
	t.Run("AlreadyConfirmed", confirmEmailAlreadyConfirmed)
	t.Run("InvalidToken", confirmEmailInvalidToken)
	t.Run("WrongSecret", confirmEmailWrongSecret)
	t.Run("MissingToken", confirmEmailMissingToken)
}

func confirmEmailSuccess(t *testing.T) {
	t.Helper()
	handler := newTestHandler()
	email := fmt.Sprintf("confirm+%d@example.com", time.Now().UnixNano())

	// Register a user.
	body, _ := json.Marshal(map[string]string{
		"email": email, "password": "Password1", "firstName": "Test", "lastName": "User",
	})
	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)
	require.Equal(t, http.StatusCreated, w.Code)

	var result register.Result
	require.NoError(t, json.NewDecoder(w.Body).Decode(&result))

	// Generate the token the same way the handler does.
	token := GenerateConfirmToken(result.Id, []byte("test-secret"))

	// Confirm email.
	req2 := httptest.NewRequest(http.MethodGet, "/confirm-email?token="+token, nil)
	w2 := httptest.NewRecorder()
	handler.ServeHTTP(w2, req2)
	require.Equal(t, http.StatusOK, w2.Code)

	// Login and verify EmailConfirmed is now true.
	loginBody, _ := json.Marshal(map[string]string{"email": email, "password": "Password1"})
	req3 := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(loginBody))
	req3.Header.Set("Content-Type", "application/json")
	w3 := httptest.NewRecorder()
	handler.ServeHTTP(w3, req3)
	require.Equal(t, http.StatusOK, w3.Code)

	var loginResp map[string]any
	require.NoError(t, json.NewDecoder(w3.Body).Decode(&loginResp))
	assert.Equal(t, true, loginResp["emailConfirmed"])
}

func confirmEmailAlreadyConfirmed(t *testing.T) {
	t.Helper()
	handler := newTestHandler()
	email := fmt.Sprintf("confirm.twice+%d@example.com", time.Now().UnixNano())

	body, _ := json.Marshal(map[string]string{
		"email": email, "password": "Password1", "firstName": "Test", "lastName": "User",
	})
	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)
	require.Equal(t, http.StatusCreated, w.Code)

	var result register.Result
	require.NoError(t, json.NewDecoder(w.Body).Decode(&result))
	token := GenerateConfirmToken(result.Id, []byte("test-secret"))

	// First confirmation.
	req2 := httptest.NewRequest(http.MethodGet, "/confirm-email?token="+token, nil)
	w2 := httptest.NewRecorder()
	handler.ServeHTTP(w2, req2)
	require.Equal(t, http.StatusOK, w2.Code)

	// Second confirmation — idempotent, should still succeed.
	req3 := httptest.NewRequest(http.MethodGet, "/confirm-email?token="+token, nil)
	w3 := httptest.NewRecorder()
	handler.ServeHTTP(w3, req3)
	assert.Equal(t, http.StatusOK, w3.Code)
}

func confirmEmailInvalidToken(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodGet, "/confirm-email?token=invalid.token", nil)
	w := httptest.NewRecorder()
	newTestHandler().ServeHTTP(w, req)
	require.Equal(t, http.StatusBadRequest, w.Code)
	assert.JSONEq(t, `{"message":"invalid or expired confirmation token"}`, w.Body.String())
}

func confirmEmailWrongSecret(t *testing.T) {
	t.Helper()
	// Token signed with a different secret fails signature verification.
	token := GenerateConfirmToken("some-user-id", []byte("wrong-secret"))
	req := httptest.NewRequest(http.MethodGet, "/confirm-email?token="+token, nil)
	w := httptest.NewRecorder()
	newTestHandler().ServeHTTP(w, req)
	require.Equal(t, http.StatusBadRequest, w.Code)
	assert.JSONEq(t, `{"message":"invalid or expired confirmation token"}`, w.Body.String())
}

func confirmEmailMissingToken(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodGet, "/confirm-email", nil)
	w := httptest.NewRecorder()
	newTestHandler().ServeHTTP(w, req)
	require.Equal(t, http.StatusBadRequest, w.Code)
	assert.JSONEq(t, `{"message":"missing token"}`, w.Body.String())
}
