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

func TestLogin(t *testing.T) {
	t.Run("Success", loginSuccess)
	t.Run("InvalidBody", loginInvalidBody)
	t.Run("InvalidCredentials", loginInvalidCredentials)
	t.Run("WrongPassword", loginWrongPassword)
	t.Run("MissingFields", loginMissingFields)
	t.Run("InvalidEmail", loginInvalidEmail)
}

// registerUser is a helper that registers a user via the API and requires success.
func registerUser(t *testing.T, handler http.Handler, email, password string) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		"email":     email,
		"password":  password,
		"firstName": "Test",
		"lastName":  "User",
	})
	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)
	require.Equal(t, http.StatusCreated, w.Code)
}

func loginSuccess(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("login.success+%d@example.com", time.Now().UnixNano())
	password := "Password1"
	handler := newTestHandler()

	registerUser(t, handler, email, password)

	body, _ := json.Marshal(map[string]any{
		"email":    email,
		"password": password,
	})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)

	require.Equal(t, http.StatusOK, w.Code)

	cookies := w.Result().Cookies()
	cookieMap := make(map[string]string)
	for _, c := range cookies {
		cookieMap[c.Name] = c.Value
	}
	assert.NotEmpty(t, cookieMap["accessToken"])
	assert.NotEmpty(t, cookieMap["refreshToken"])

	var result map[string]any
	require.NoError(t, json.NewDecoder(w.Body).Decode(&result))
	assert.Equal(t, email, result["email"])
	assert.Equal(t, "Test", result["firstName"])
	assert.NotEmpty(t, result["id"])
}

func loginInvalidBody(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader([]byte("not-json")))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusBadRequest, w.Code)
	assert.JSONEq(t, `{"message":"invalid request body"}`, w.Body.String())
}

func loginInvalidCredentials(t *testing.T) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		"email":    "nonexistent@example.com",
		"password": "Password1",
	})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"invalid credentials"}`, w.Body.String())
}

func loginWrongPassword(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("login.wrongpw+%d@example.com", time.Now().UnixNano())
	handler := newTestHandler()

	registerUser(t, handler, email, "Password1")

	body, _ := json.Marshal(map[string]string{
		"email":    email,
		"password": "WrongPassword1",
	})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"invalid credentials"}`, w.Body.String())
}

func loginMissingFields(t *testing.T) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		// email and password omitted
	})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnprocessableEntity, w.Code)
	assert.JSONEq(t, `[{"name":"email","errors":["email is a required field"]},{"name":"password","errors":["password is a required field"]}]`, w.Body.String())
}

func loginInvalidEmail(t *testing.T) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		"email":    "not-an-email",
		"password": "Password1",
	})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnprocessableEntity, w.Code)
	assert.JSONEq(t, `[{"name":"email","errors":["email must be a valid email address"]}]`, w.Body.String())
}
