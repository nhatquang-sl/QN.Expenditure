package controllertests

import (
	"auth/cmd/controllers"
	"auth/cmd/middleware"
	"auth/internal/application/register"
	"auth/internal/application/shared"
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

type mockEmailService struct {
	userId    string
	emailType shared.EmailType
	called    bool
}

func (m *mockEmailService) Send(_ context.Context, userId string, emailType shared.EmailType, _ any) error {
	m.userId = userId
	m.emailType = emailType
	m.called = true
	return nil
}

// TestRegister groups all register endpoint cases. Each case is extracted into
// a helper function marked with t.Helper() so that on failure, the reported
// line points to the t.Run call here rather than inside the helper.
func TestRegister(t *testing.T) {
	t.Run("Success", registerSuccess)
	t.Run("DuplicateEmail", registerDuplicateEmail)
	t.Run("WeakPassword", registerWeakPassword)
	t.Run("MissingFields", registerMissingFields)
	t.Run("InvalidBody", registerInvalidBody)
	t.Run("AssignsUserRole", registerAssignsUserRole)
	t.Run("SendsActivationEmail", registerSendsActivationEmail)
}

func registerSuccess(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("john.doe+%d@example.com", time.Now().UnixNano())
	body, _ := json.Marshal(map[string]string{
		"email":     email,
		"password":  "Password1",
		"firstName": "John",
		"lastName":  "Doe",
	})

	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusCreated, w.Code)
	var resp register.Result
	require.NoError(t, json.NewDecoder(w.Body).Decode(&resp))
	assert.Equal(t, email, resp.Email)
	assert.Equal(t, "John", resp.FirstName)
	assert.NotEmpty(t, resp.Id)
}

func registerDuplicateEmail(t *testing.T) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		"email":     fmt.Sprintf("duplicate+%d@example.com", time.Now().UnixNano()),
		"password":  "Password1",
		"firstName": "Jane",
		"lastName":  "Doe",
	})

	handler := newTestHandler()

	// First registration — should succeed.
	req1 := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req1.Header.Set("Content-Type", "application/json")
	w1 := httptest.NewRecorder()
	handler.ServeHTTP(w1, req1)
	require.Equal(t, http.StatusCreated, w1.Code)

	// Second registration with the same email — should conflict.
	req2 := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req2.Header.Set("Content-Type", "application/json")
	w2 := httptest.NewRecorder()
	handler.ServeHTTP(w2, req2)
	require.Equal(t, http.StatusConflict, w2.Code)
	assert.JSONEq(t, `{"message":"email already registered"}`, w2.Body.String())
}

func registerWeakPassword(t *testing.T) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		"email":     "weak@example.com",
		"password":  "weak", // fails: too short, no uppercase, no digit
		"firstName": "Test",
		"lastName":  "User",
	})

	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnprocessableEntity, w.Code)
	assert.JSONEq(t, `[{"name":"password","errors":["password must be at least 8 characters with an uppercase letter, a lowercase letter, and a digit"]}]`, w.Body.String())
}

func registerInvalidBody(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader([]byte("not-json")))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusBadRequest, w.Code)
	assert.JSONEq(t, `{"message":"invalid request body"}`, w.Body.String())
}

func registerSendsActivationEmail(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("register.email+%d@example.com", time.Now().UnixNano())

	mock := &mockEmailService{}
	mux := http.NewServeMux()
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	controllers.NewAuthController(mux, testQueries, testCache, testJwtService, mock, logger, "test-secret", "http://localhost", true)
	handler := middleware.Recover(logger, mux)

	body, _ := json.Marshal(map[string]string{
		"email":     email,
		"password":  "Password1",
		"firstName": "Test",
		"lastName":  "User",
	})
	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)

	require.Equal(t, http.StatusCreated, w.Code)

	var result register.Result
	require.NoError(t, json.NewDecoder(w.Body).Decode(&result))

	assert.True(t, mock.called, "EmailService.Send should have been called")
	assert.Equal(t, result.Id, mock.userId)
	assert.Equal(t, shared.EmailTypeActivateAccount, mock.emailType)
}

func registerAssignsUserRole(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("register.role+%d@example.com", time.Now().UnixNano())
	handler := newTestHandler()
	accessToken := loginUser(t, handler, email, "Password1")

	claims, err := testJwtService.ValidateAccessToken(accessToken)
	require.NoError(t, err)
	require.NotNil(t, claims)
	assert.Equal(t, []string{"user"}, claims.Roles)
}

func registerMissingFields(t *testing.T) {
	t.Helper()
	body, _ := json.Marshal(map[string]string{
		"email": "missing@example.com",
		// password, firstName, lastName omitted
	})

	req := httptest.NewRequest(http.MethodPost, "/register", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnprocessableEntity, w.Code)
	assert.JSONEq(t, `[{"name":"firstName","errors":["firstName is a required field"]},{"name":"lastName","errors":["lastName is a required field"]},{"name":"password","errors":["password is a required field"]}]`, w.Body.String())
}
