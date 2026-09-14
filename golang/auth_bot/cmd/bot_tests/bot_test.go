package bottests

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"testing"

	"auth_bot/internal/bot"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

type capturedRequest struct {
	method  string
	path    string
	body    map[string]any
	cookies map[string]string
}

type stubServer struct {
	mu       sync.Mutex
	calls    []capturedRequest
	handlers map[string]http.HandlerFunc
}

func newStubServer(t *testing.T) (*stubServer, *httptest.Server) {
	t.Helper()
	s := &stubServer{handlers: make(map[string]http.HandlerFunc)}
	srv := httptest.NewServer(s)
	t.Cleanup(srv.Close)
	return s, srv
}

func (s *stubServer) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	bodyBytes, _ := io.ReadAll(r.Body)
	var body map[string]any
	_ = json.Unmarshal(bodyBytes, &body)

	cookies := make(map[string]string)
	for _, c := range r.Cookies() {
		cookies[c.Name] = c.Value
	}

	s.mu.Lock()
	s.calls = append(s.calls, capturedRequest{method: r.Method, path: r.URL.Path, body: body, cookies: cookies})
	s.mu.Unlock()

	key := r.Method + " " + r.URL.Path
	if h, ok := s.handlers[key]; ok {
		h(w, r)
	} else {
		w.WriteHeader(http.StatusNotFound)
	}
}

func (s *stubServer) captured() []capturedRequest {
	s.mu.Lock()
	defer s.mu.Unlock()
	out := make([]capturedRequest, len(s.calls))
	copy(out, s.calls)
	return out
}

func newBot(t *testing.T, srv *httptest.Server) *bot.Bot {
	t.Helper()
	return bot.New(srv.URL, "TestP@ss123!", srv.Client(), slog.New(slog.NewTextHandler(io.Discard, nil)))
}

func TestRegister(t *testing.T) {
	t.Run("Success", testRegisterSuccess)
	t.Run("Non201SkipsAccumulation", testRegisterNon201SkipsAccumulation)
}

// testRegisterSuccess: server returns 201 → bot makes a well-formed request and accumulates the user.
func testRegisterSuccess(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)
	stub.handlers["POST /register"] = func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusCreated)
		_, _ = w.Write([]byte(`{"id":"abc","email":"bot+1@yopmail.com","firstName":"Bot","lastName":"User"}`))
	}

	b := newBot(t, srv)
	b.Register(context.Background())
	b.Register(context.Background())

	calls := stub.captured()
	require.Len(t, calls, 2)

	for _, c := range calls {
		assert.Equal(t, "POST", c.method)
		assert.Equal(t, "/register", c.path)

		email, ok := c.body["email"].(string)
		require.True(t, ok, "email field must be a string")
		assert.True(t, strings.HasPrefix(email, "bot"), "email should start with bot")
		assert.True(t, strings.HasSuffix(email, "@yopmail.com"), "email should end with @yopmail.com")
		assert.Equal(t, "TestP@ss123!", c.body["password"])
		assert.Equal(t, "Bot", c.body["firstName"])
		assert.Equal(t, "User", c.body["lastName"])
	}

	assert.Equal(t, 2, b.UserCount())
}

// testRegisterNon201SkipsAccumulation: server returns 409 → bot does not accumulate the user.
func testRegisterNon201SkipsAccumulation(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)
	stub.handlers["POST /register"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusConflict)
	}

	b := newBot(t, srv)
	b.Register(context.Background())

	require.Len(t, stub.captured(), 1)
	assert.Equal(t, 0, b.UserCount())
}

func TestLogin(t *testing.T) {
	t.Run("NoUsersSkips", testLoginNoUsersSkips)
	t.Run("Success", testLoginSuccess)
	t.Run("LoginFail", testLoginLoginFail)
	t.Run("RefreshFail", testLoginRefreshFail)
	t.Run("MultiUser", testLoginMultiUser)
}

// testLoginNoUsersSkips: no registered users → Login makes zero HTTP calls.
func testLoginNoUsersSkips(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)
	b := newBot(t, srv)

	b.Login(context.Background())

	assert.Empty(t, stub.captured())
}

// testLoginSuccess: full cycle — login → refresh → profile — all three calls made in order
// with the correct cookies forwarded at each step.
func testLoginSuccess(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)

	stub.handlers["POST /register"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusCreated)
	}
	stub.handlers["POST /login"] = func(w http.ResponseWriter, r *http.Request) {
		http.SetCookie(w, &http.Cookie{Name: "accessToken", Value: "access-1"})
		http.SetCookie(w, &http.Cookie{Name: "refreshToken", Value: "refresh-1"})
		w.WriteHeader(http.StatusOK)
	}
	stub.handlers["POST /refresh-token"] = func(w http.ResponseWriter, r *http.Request) {
		http.SetCookie(w, &http.Cookie{Name: "accessToken", Value: "access-2"})
		http.SetCookie(w, &http.Cookie{Name: "refreshToken", Value: "refresh-2"})
		w.WriteHeader(http.StatusOK)
	}
	stub.handlers["GET /profile"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	}

	b := newBot(t, srv)
	b.Register(context.Background())
	b.Login(context.Background())

	calls := stub.captured()
	require.Len(t, calls, 4) // register + login + refresh + profile

	login := calls[1]
	assert.Equal(t, "POST", login.method)
	assert.Equal(t, "/login", login.path)

	refresh := calls[2]
	assert.Equal(t, "POST", refresh.method)
	assert.Equal(t, "/refresh-token", refresh.path)
	assert.Equal(t, "refresh-1", refresh.cookies["refreshToken"])

	profile := calls[3]
	assert.Equal(t, "GET", profile.method)
	assert.Equal(t, "/profile", profile.path)
	assert.Equal(t, "access-2", profile.cookies["accessToken"]) // uses refreshed token
}

// testLoginLoginFail: login returns non-200 → only one call made, no refresh or profile.
func testLoginLoginFail(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)

	stub.handlers["POST /register"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusCreated)
	}
	stub.handlers["POST /login"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusUnauthorized)
	}

	b := newBot(t, srv)
	b.Register(context.Background())
	b.Login(context.Background())

	calls := stub.captured()
	require.Len(t, calls, 2) // register + login only
	assert.Equal(t, "/login", calls[1].path)
}

// testLoginRefreshFail: login succeeds but refresh returns non-200 → profile still called with original access token.
// User at index 0: 0%3==0 → refresh attempted; 0%2==0 → profile called regardless.
func testLoginRefreshFail(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)

	stub.handlers["POST /register"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusCreated)
	}
	stub.handlers["POST /login"] = func(w http.ResponseWriter, r *http.Request) {
		http.SetCookie(w, &http.Cookie{Name: "accessToken", Value: "access-1"})
		http.SetCookie(w, &http.Cookie{Name: "refreshToken", Value: "refresh-1"})
		w.WriteHeader(http.StatusOK)
	}
	stub.handlers["POST /refresh-token"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusUnauthorized)
	}
	stub.handlers["GET /profile"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	}

	b := newBot(t, srv)
	b.Register(context.Background())
	b.Login(context.Background())

	calls := stub.captured()
	require.Len(t, calls, 4) // register + login + refresh + profile
	assert.Equal(t, "/refresh-token", calls[2].path)
	assert.Equal(t, "/profile", calls[3].path)
	assert.Equal(t, "access-1", calls[3].cookies["accessToken"]) // original token used (refresh failed)
}

// testLoginMultiUser: three users registered, one Login() call → all three receive /login,
// index-based conditions determine which get /refresh-token and /profile.
// i=0: 0%3==0 → refresh; 0%2==0 → profile
// i=1: 1%3!=0 → no refresh; 1%2!=0 → no profile
// i=2: 2%3!=0 → no refresh; 2%2==0 → profile
func testLoginMultiUser(t *testing.T) {
	t.Helper()
	stub, srv := newStubServer(t)

	stub.handlers["POST /register"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusCreated)
	}
	stub.handlers["POST /login"] = func(w http.ResponseWriter, r *http.Request) {
		http.SetCookie(w, &http.Cookie{Name: "accessToken", Value: "at"})
		http.SetCookie(w, &http.Cookie{Name: "refreshToken", Value: "rt"})
		w.WriteHeader(http.StatusOK)
	}
	stub.handlers["POST /refresh-token"] = func(w http.ResponseWriter, r *http.Request) {
		http.SetCookie(w, &http.Cookie{Name: "accessToken", Value: "at2"})
		http.SetCookie(w, &http.Cookie{Name: "refreshToken", Value: "rt2"})
		w.WriteHeader(http.StatusOK)
	}
	stub.handlers["GET /profile"] = func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	}

	b := newBot(t, srv)
	b.Register(context.Background())
	b.Register(context.Background())
	b.Register(context.Background())
	require.Equal(t, 3, b.UserCount())

	b.Login(context.Background())

	calls := stub.captured()
	// 3 registers + 3 logins + 1 refresh (i=0) + 2 profiles (i=0, i=2) = 9
	require.Len(t, calls, 9)

	var loginPaths, refreshPaths, profilePaths []capturedRequest
	for _, c := range calls[3:] { // skip the 3 register calls
		switch {
		case c.path == "/login":
			loginPaths = append(loginPaths, c)
		case c.path == "/refresh-token":
			refreshPaths = append(refreshPaths, c)
		case c.path == "/profile":
			profilePaths = append(profilePaths, c)
		}
	}

	assert.Len(t, loginPaths, 3, "all 3 users should be logged in")
	assert.Len(t, refreshPaths, 1, "only user at index 0 (0%%3==0) gets refresh")
	assert.Len(t, profilePaths, 2, "users at index 0 and 2 (i%%2==0) get profile")
}
