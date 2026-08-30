package controllertests

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	getprofile "auth/internal/application/get_profile"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestGetProfile(t *testing.T) {
	t.Run("Success", getProfileSuccess)
	t.Run("CacheHit", getProfileCacheHit)
	t.Run("NoCookie", getProfileNoCookie)
	t.Run("InvalidToken", getProfileInvalidToken)
}

// loginUser is a helper that registers and logs in a user, returning the access token.
func loginUser(t *testing.T, handler http.Handler, email, password string) string {
	t.Helper()
	registerUser(t, handler, email, password)

	body, _ := json.Marshal(map[string]string{
		"email":    email,
		"password": password,
	})
	req := httptest.NewRequest(http.MethodPost, "/login", bytes.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)
	require.Equal(t, http.StatusOK, w.Code)

	for _, c := range w.Result().Cookies() {
		if c.Name == "accessToken" {
			return c.Value
		}
	}
	t.Fatal("accessToken cookie not found in login response")
	return ""
}

func getProfileSuccess(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("profile.success+%d@example.com", time.Now().UnixNano())
	handler := newTestHandler()
	accessToken := loginUser(t, handler, email, "Password1")

	req := httptest.NewRequest(http.MethodGet, "/profile", nil)
	req.AddCookie(&http.Cookie{Name: "accessToken", Value: accessToken})
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, req)

	require.Equal(t, http.StatusOK, w.Code)
	var result getprofile.Result
	require.NoError(t, json.NewDecoder(w.Body).Decode(&result))
	assert.Equal(t, email, result.Email)
	assert.Equal(t, "Test", result.FirstName)
	assert.Equal(t, "User", result.LastName)
	assert.NotEmpty(t, result.Id)
}

func getProfileCacheHit(t *testing.T) {
	t.Helper()
	email := fmt.Sprintf("profile.cache+%d@example.com", time.Now().UnixNano())
	handler := newTestHandler()
	accessToken := loginUser(t, handler, email, "Password1")

	doRequest := func() getprofile.Result {
		req := httptest.NewRequest(http.MethodGet, "/profile", nil)
		req.AddCookie(&http.Cookie{Name: "accessToken", Value: accessToken})
		w := httptest.NewRecorder()
		handler.ServeHTTP(w, req)
		require.Equal(t, http.StatusOK, w.Code)
		var result getprofile.Result
		require.NoError(t, json.NewDecoder(w.Body).Decode(&result))
		return result
	}

	first := doRequest() // DB hit — response cached in Redis

	// Delete the user from the DB. If the cache is working, the second request
	// must still return the profile served from Redis.
	_, err := testDB.ExecContext(t.Context(), `DELETE FROM "Users" WHERE "Email" = $1`, email)
	require.NoError(t, err)

	second := doRequest() // must come from cache — DB row is gone

	assert.Equal(t, first, second)
}

func getProfileNoCookie(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodGet, "/profile", nil)
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"missing access token"}`, w.Body.String())
}

func getProfileInvalidToken(t *testing.T) {
	t.Helper()
	req := httptest.NewRequest(http.MethodGet, "/profile", nil)
	req.AddCookie(&http.Cookie{Name: "accessToken", Value: "invalid.token.value"})
	w := httptest.NewRecorder()

	newTestHandler().ServeHTTP(w, req)

	require.Equal(t, http.StatusUnauthorized, w.Code)
	assert.JSONEq(t, `{"message":"invalid access token"}`, w.Body.String())
}
