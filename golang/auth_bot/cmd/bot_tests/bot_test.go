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
	method string
	path   string
	body   map[string]any
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

	s.mu.Lock()
	s.calls = append(s.calls, capturedRequest{method: r.Method, path: r.URL.Path, body: body})
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
		assert.True(t, strings.HasPrefix(email, "bot+"), "email should start with bot+")
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
