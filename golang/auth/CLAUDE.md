# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
make dev           # run with live-reload (air), requires CONFIG_PATH env var
make run           # run without live-reload
make build         # compile to ./bin/gobin
make test          # run tests verbosely
make coverage      # run tests + print per-function coverage
make coverage-html # run tests + open HTML coverage report in browser
make fmt           # go fmt ./...
make deps          # go mod tidy
make migrate-create n=<name>  # create a new migration file
make migrate-up    # apply migrations (requires POSTGRESQL_URL env var)
```

**Run a single test:**
```bash
go test ./cmd/controller_tests/... -run TestLogin/Success -v
```

**Regenerate sqlc after changing `.sql` files:**
```bash
sqlc generate -f internal/database/sqlc.yaml
```
Generated files land in `internal/database/generated/` (gitignored).

---

## Directory Structure

```
golang/auth/
├── cmd/
│   ├── main.go                                  # Wiring, migrations, server startup, session cleaner goroutine
│   ├── controllers/
│   │   ├── auth_controller.go                   # All auth routes; sets/clears token cookies
│   │   └── health_controller.go                 # GET /health
│   ├── middleware/
│   │   ├── auth.go                              # JWT validation + Redis revocation check
│   │   └── recover.go                           # Global panic recovery (delegates to shared/httpx)
│   ├── respond/
│   │   └── response.go                          # JSON(status, result, err) — used in every handler
│   └── controller_tests/
│       ├── main_test.go                         # TestMain: testcontainers (Postgres, Redis, RabbitMQ) + newTestHandler()
│       ├── register_test.go
│       ├── login_test.go
│       ├── logout_test.go
│       ├── refresh_token_test.go
│       ├── confirm_email_test.go
│       ├── get_profile_test.go
│       └── session_cleaner_test.go
├── internal/
│   ├── config/
│   │   └── config.go                            # Config struct + LoadJSONConfig(); env vars override file values
│   ├── application/
│   │   ├── app_validator.go                     # Handler[C,R] interface + Validator[C,R] wrapper
│   │   ├── app_cacher.go                        # Cacher[C,R] wrapper — Redis get-or-create with hit/miss logging
│   │   ├── shared/
│   │   │   ├── jwt.go                           # JwtService interface, UserClaims, TokenPair
│   │   │   ├── token.go                         # GenerateConfirmToken / ParseConfirmToken (HMAC-SHA256)
│   │   │   └── roles.go                         # RoleAdmin = "admin", RoleUser = "user"
│   │   ├── health/handler.go                    # Always returns {status: "healthy"}
│   │   ├── login/
│   │   │   ├── handler.go                       # Verify password, create session, generate JWT pair
│   │   │   ├── validator.go
│   │   │   └── login.sql
│   │   ├── register/
│   │   │   ├── handler.go                       # Hash password, create user, publish email via RabbitMQ
│   │   │   ├── validator.go                     # Custom password_strength rule
│   │   │   └── register.sql
│   │   ├── confirm_email/
│   │   │   ├── handler.go                       # Parse HMAC token, set EmailConfirmed = true
│   │   │   └── confirm_email.sql
│   │   ├── refresh_token/
│   │   │   ├── handler.go                       # Validate refresh JWT, rotate token pair
│   │   │   └── refresh_token.sql
│   │   ├── logout/
│   │   │   ├── handler.go                       # Delete session, write revocation key to Redis
│   │   │   └── logout.sql
│   │   ├── get_profile/
│   │   │   ├── handler.go                       # Fetch user + roles; wrapped with Cacher (key: profile:<userId>)
│   │   │   └── get_profile.sql
│   │   └── session_cleaner/
│   │       ├── cleaner.go                       # Delete stale sessions/histories older than 7 days (max 10k/run)
│   │       └── session_cleaner.sql
│   ├── database/
│   │   ├── embed.go                             # Embeds migrations + schema as FS for golang-migrate
│   │   ├── migrations/                          # Applied at startup via runMigrations() in main.go
│   │   ├── schema/schema.sql                    # Table snapshots for sqlc type inference ONLY — never applied
│   │   └── generated/                           # gitignored; regenerate with sqlc generate
│   └── services/
│       ├── jwt/service.go                       # HS256 sign/validate; implements JwtService
│       └── redis/redis_service.go               # GetOrCreate, GetOrCreateDefault, Set, Exists, Delete
└── go.mod
```

---

## Key Packages

| Package | Purpose |
|---|---|
| `github.com/lib/pq` | PostgreSQL driver (via `database/sql`) |
| `sqlc` (dev tool) | Generates type-safe Go from `.sql` files into `internal/database/generated/` |
| `github.com/golang-jwt/jwt/v5` | Signs/validates HS256 tokens |
| `github.com/go-playground/validator/v10` | Struct-tag validation with English translations |
| `github.com/golang-migrate/migrate/v4` | DB migrations (`make migrate-up`) |
| `github.com/testcontainers/testcontainers-go` | Real PostgreSQL/Redis/RabbitMQ containers for integration tests |
| `cosmtrek/air` | Live-reload for `make dev` |
| `golang.org/x/crypto/pbkdf2` | Password hashing (ASP.NET Identity V3 format) |
| `github.com/google/uuid` | User IDs |
| `go.opentelemetry.io/*` | Distributed tracing |

---

## Non-Obvious Folder Locations

- `cmd/controller_tests/` — all integration tests (not in `cmd/controllers/`)
- `internal/application/shared/` — `JwtService` interface, `UserClaims`, `TokenPair`, token helpers, role constants
- `internal/application/app_cacher.go` — `Cacher[C,R]` Redis caching wrapper
- `internal/services/jwt/service.go` — `jwt.Service` implementing `shared.JwtService`
- `internal/database/migrations/` — golang-migrate SQL files (applied at startup, not via `make migrate-up` in production)
- `internal/database/schema/` — table snapshots for sqlc type inference only; never applied to the DB
- `internal/database/generated/` — gitignored; regenerate with `sqlc generate -f internal/database/sqlc.yaml`

---

## Core Architecture Principle

**`internal/application/` must have zero `net/http` imports — ever.**

Everything HTTP-related lives exclusively in `cmd/`:
- `cmd/controllers/` — route registration, JSON decoding, calling handlers, JSON encoding, cookie setting
- `cmd/middleware/` — HTTP middleware (`Recover`, `Auth`)
- `cmd/respond/` — writing HTTP responses
- `cmd/main.go` — wiring and server startup

Application handlers in `internal/application/` receive and return plain Go types only. They have no knowledge of HTTP requests, responses, status codes, or cookies. The `cmd` layer is entirely responsible for translating between HTTP and the application layer.

---

## Layer Design

### `internal/application/` — pure business logic

Each feature is a sub-package with a `Handler[C, R]` implementation:

```go
// Handler interface — implemented by every feature handler
type Handler[C, R any] interface {
    Handle(ctx context.Context, cmd C) (R, error)
}
```

`NewHandler(...)` in each feature package wraps the real handler in `Validator[C, R]` (defined in `internal/application/app_validator.go`), which runs struct validation before delegating:

```go
// Validator runs validation, returns *apperror.ValidationError on failure,
// then delegates to the inner handler which returns *apperror.AppError on business failures.
func (vl *Validator[C, R]) Handle(ctx context.Context, cmd C) (R, error) { ... }
```

**`Cacher[C, R]`** — a second wrapper for Redis caching, defined in `internal/application/app_cacher.go`:

```go
// NewCacher wraps inner with a Redis cache layer.
// Results are keyed by keyFn(cmd) using the service's default TTL.
func NewCacher[C, R any](inner Handler[C, R], cache *RedisService, keyFn func(C) string) Handler[C, R]
```

Currently used by `get_profile` (cache key: `profile:<userId>`). Logs cache hit/miss via `slog`.

### `cmd/controllers/` — HTTP layer

Controllers receive `*http.ServeMux` in their constructor and self-register their routes. They:
1. Decode the JSON request body (or read cookies for token endpoints)
2. Call the application handler
3. Write the response via `respond.NewResponse(w, logger).JSON(status, result, err)`

Nothing from `net/http` leaks into `internal/application/`.

### `cmd/middleware/` — HTTP middleware

- `Recover(logger, next)` — wraps the entire mux globally. Catches panics, logs them, and writes a JSON error response.
- `Auth(jwtService, redisService, logger)` — applied per-route inside controllers. Reads the `accessToken` cookie, validates it, checks Redis for revocation, stores `*shared.UserClaims` in context via `UserFromContext()`, or writes `401` and returns.

---

## Feature Slices Reference

| Feature | Route | Auth required | External deps |
|---|---|---|---|
| `login` | `POST /login` | No | DB, JWT |
| `register` | `POST /register` | No | DB, RabbitMQ |
| `confirm_email` | `GET /confirm-email?token=` | No | DB |
| `refresh_token` | `POST /refresh-token` | No | DB, JWT |
| `logout` | `POST /logout` | Yes | DB, Redis (revocation write) |
| `get_profile` | `GET /profile` | Yes | DB, Redis (cache read) |
| `health` | `GET /health` | No | — |
| `session_cleaner` | background goroutine | — | DB |

`confirm_email` reads the token from the query string (`?token=`), not the body.
`refresh_token` and `logout` read the `refreshToken` cookie; if missing, the controller returns 401 directly without calling the handler.
`IPAddress` and `UserAgent` are injected by the controller from request headers before calling `login` and `refresh_token` handlers.

---

## Cookie Handling

`setTokenCookies` and `clearTokenCookies` live in `auth_controller.go`. Cookies are set on login and refresh; cleared (MaxAge=-1) on logout.

| Cookie | HttpOnly | Secure | SameSite | MaxAge |
|---|---|---|---|---|
| `accessToken` | Yes | Yes* | Strict | 5 minutes |
| `refreshToken` | Yes | Yes* | Strict | 5 hours (or 30 days if rememberMe) |

*Secure is disabled when `APP_ENV=Development`.

Auth middleware reads the `accessToken` cookie — not the `Authorization` header.

---

## Password Hashing

Passwords are hashed with PBKDF2-SHA256 in **ASP.NET Identity V3 format** for cross-service compatibility:

- Format byte: `0x01` (V3)
- PRF identifier: `0x00000001` (SHA256)
- Iterations: 100,000
- Salt: 16 bytes (random)
- Subkey: 32 bytes
- Total binary payload: 49 bytes → base64 encoded

This allows users created in the Go service to authenticate in the .NET service and vice versa.

---

## Email Confirmation Tokens

Stateless HMAC-SHA256 tokens — no DB entry required. Defined in `internal/application/shared/token.go`.

**Format:** `base64url(userID:expiry_unix)` + `.` + `base64url(HMAC-SHA256(payload, secret))`

```go
token := GenerateConfirmToken(userID, secret)   // 24h expiry
userID, err := ParseConfirmToken(token, secret)  // validates sig + expiry
```

- TTL: 24 hours
- Secret source: `TOKEN_SECRET` env var (passed through config to handlers)
- Email link: `{APPLICATION_ENDPOINT}/confirm-email?token={token}`

---

## JWT Token Types

`GenerateTokens()` sets a `type` claim on both access and refresh tokens:

| Type | When set |
|---|---|
| `"LOGIN"` | `EmailConfirmed == true` |
| `"NEED_ACTIVATE"` | `EmailConfirmed == false` |

Token expiry:
- Access token: **5 minutes**
- Refresh token: **5 hours** (or **30 days** if `rememberMe == true`)

JWT claims include: `id`, `email`, `firstName`, `lastName`, `emailConfirmed`, `type`, `tokenId` (session history ID used for revocation), `roles`, `iss`, `aud`, `exp`, `iat`.

---

## Redis Session Revocation

Logged-out sessions are recorded in Redis to enable immediate token invalidation. Nothing is written on login.

### Key Design

- **Key**: `revoked:<tokenId>` where `tokenId` = `UserSessions.Id` (int64)
- **Value**: `"1"` — only existence matters
- **TTL**: remaining refresh token lifetime, read from the refresh token's `exp` claim at logout time

### Flow

- **Login** (`internal/application/login/handler.go`): no Redis write.
- **Logout** (`internal/application/logout/handler.go`): decodes the refresh token to get `tokenId` and `exp`, deletes the `UserSessions` row, writes `revoked:<tokenId>` with TTL = `time.Until(exp)`. If the refresh token is missing or invalid, logout still succeeds silently — no revocation entry is written, but the token is already naturally invalid.
- **Auth middleware** (`cmd/middleware/auth.go`): after JWT validation, call `cache.Exists("revoked:<tokenId>")`:
  - Key present → 401 `"session invalidated"`
  - Redis error → skip check, allow request (graceful degradation)
  - `cache == nil` → skip check (tests or degraded deployments)

---

## Session Cleaner

A background goroutine started in `main.go` on server startup:

- Runs **immediately** at startup, then **every hour**
- Deletes `UserSessionHistories` rows older than 7 days (max 10,000 rows per run)
- Deletes `UserSessions` rows older than 7 days (max 10,000 rows per run)
- Implemented as `Handler[Command, Result]` in `internal/application/session_cleaner/cleaner.go`

---

## Environment Variables

These override the JSON config file values:

| Variable | Purpose |
|---|---|
| `CONFIG_PATH` | Path to JSON config file |
| `PG_AUTH_CONNECTION` | PostgreSQL connection string |
| `VERSION` | Application version label |
| `APPLICATION_ENDPOINT` | Base URL for email confirmation links |
| `REDIS_ADDR` | Redis address (e.g. `localhost:6379`) |
| `REDIS_PASSWORD` | Redis password |
| `RABBITMQ_HOST` | RabbitMQ host |
| `RABBITMQ_USERNAME` | RabbitMQ username |
| `RABBITMQ_PASSWORD` | RabbitMQ password |
| `TOKEN_SECRET` | HMAC secret for email confirmation tokens |
| `APP_ENV` | Set to `"Development"` to disable Secure cookie flag |
| `OTEL_SERVICE_NAME` | OpenTelemetry service name for tracing |

---

## Error Handling

### Application → controller path (normal flow)
Application handlers return typed errors. Controllers pass them through to `respond.JSON`, which maps them to HTTP responses:

| Error type | HTTP status | Body |
|---|---|---|
| `*apperror.AppError` | `e.Code` (400/401/404/409) | `{"message": "..."}` |
| `*apperror.ValidationError` | 422 | `[{"name":"field","errors":["msg"]}]` |
| any other `error` | 500 | `{"message": "Internal Server Error"}` |
| `nil` | caller's `status` arg | encoded `result` |

### `respond.NewResponse(w, logger).JSON(status, result, err)`
The three-arg form is used everywhere. When `err != nil`, the `status` argument is ignored — the error drives the response. Never call `WriteHeader` or set headers directly in controllers.

```go
result, err := c.login.Handle(r.Context(), cmd)
respond.NewResponse(w, c.logger).JSON(http.StatusOK, result, err)
```

### Decode errors (bad JSON body)
Controllers handle these directly — write the response and return. Do **not** panic:

```go
if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
    respond.NewResponse(w, c.logger).JSON(http.StatusBadRequest, nil, apperror.NewBadRequest("invalid request body"))
    return
}
```

### Panics
`middleware.Recover` catches genuine unexpected panics (nil pointer, index out of range) and maps them to 500. Do not use panic as a normal control-flow mechanism for business errors.

---

## Go Naming Conventions

### Package names
Go package names must be **single lowercase words — no underscores, no hyphens**.

Feature directories can use underscores (e.g. `get_profile/`, `refresh_token/`), but the `package` declaration inside must drop them:

```go
// directory: internal/application/get_profile/
package getprofile  // correct

// directory: internal/application/refresh_token/
package refreshtoken  // correct
```

When importing a package whose directory name has underscores, use an alias to keep the call-site readable:

```go
import (
    getprofile   "auth/internal/application/get_profile"
    refreshtoken "auth/internal/application/refresh_token"
)
```

---

## Adding a New Feature Slice

For slice-specific requirements (business rules, JWT/cookie settings, password hash format, DB schema notes), read `SPEC.md` before starting.

1. Create `internal/application/<feature>/handler.go` — implement `Handler[Command, Result]`; wrap with `NewValidator` in `NewHandler`
2. Create `internal/application/<feature>/<feature>.sql` — sqlc-annotated SQL queries. Always list columns explicitly — never use `SELECT *` or `RETURNING *`.
3. Run `sqlc generate -f internal/database/sqlc.yaml`
4. Add the route to a controller in `cmd/controllers/` (or create a new one)
5. Register the controller in `cmd/main.go` (one line)
6. Add tests in `cmd/controller_tests/`

To add Redis caching to a handler, wrap it with `NewCacher`:

```go
// in get_profile/handler.go — example
func NewHandler(db *dbsqlc.Queries, cache *redis.RedisService, logger *slog.Logger) Handler[Query, Result] {
    inner := newInnerHandler(db, logger)
    return application.NewCacher(inner, cache, func(q Query) string {
        return fmt.Sprintf("profile:%s", q.UserId)
    })
}
```

---

## Tests

All tests live in `cmd/controller_tests/` and are integration tests against real containers (via `testcontainers-go`). A single set of containers is shared across the package via `TestMain`.

`newTestHandler()` wires real controllers, real application handlers, a real JWT service (test secrets), real Redis, real RabbitMQ, and `Recover` middleware against the test DB — the full stack.

Shared helpers:
- `registerUser(t, handler, email, password)` — registers via HTTP
- `loginUser(t, handler, email, password) string` — registers + logs in, returns the `accessToken` cookie value

---

## Key Files

| File | Why read it first |
|---|---|
| `cmd/respond/response.go` | `JSON(status, result, err)` — the one method used in every controller |
| `internal/application/app_validator.go` | `Handler[C,R]` interface and `Validator[C,R]` wrapper |
| `internal/application/app_cacher.go` | `Cacher[C,R]` Redis caching wrapper |
| `internal/application/shared/jwt.go` | `JwtService` interface, `UserClaims`, `TokenPair` |
| `internal/application/shared/token.go` | `GenerateConfirmToken` / `ParseConfirmToken` |
| `internal/application/apperror/errors.go` | `AppError` and `ValidationError` types |
| `cmd/middleware/auth.go` + `recover.go` | how HTTP middleware works |
| `cmd/controllers/auth_controller.go` | route registration, cookie handling, reference controller |
| `cmd/controller_tests/main_test.go` | test container setup and `newTestHandler()` |
| `internal/application/register/handler.go` | reference implementation of a full feature slice |
| `internal/services/redis/redis_service.go` | cache + revocation operations |
| `internal/services/jwt/service.go` | token generation, expiry constants, JWT claims structure |
| `cmd/main.go` | full wiring, migration runner, session cleaner startup |
