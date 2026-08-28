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

## Key Packages

| Package | Purpose |
|---|---|
| `github.com/lib/pq` | PostgreSQL driver (via `database/sql`) |
| `sqlc` (dev tool) | Generates type-safe Go from `.sql` files into `internal/database/generated/` |
| `github.com/golang-jwt/jwt/v5` | Signs/validates HS256 tokens |
| `github.com/go-playground/validator/v10` | Struct-tag validation with English translations |
| `github.com/golang-migrate/migrate/v4` | DB migrations (`make migrate-up`) |
| `github.com/testcontainers/testcontainers-go` | Real PostgreSQL container for integration tests |
| `cosmtrek/air` | Live-reload for `make dev` |

---

## Non-Obvious Folder Locations

- `cmd/controller_tests/` — all integration tests (not in `cmd/controllers/`)
- `internal/services/jwt/service.go` — `jwt.Service` implementing `shared.JwtService`
- `internal/database/migrations/` — golang-migrate SQL files (applied via `make migrate-up`)
- `internal/database/schema/` — table snapshots for sqlc type inference only; never applied to the DB
- `internal/database/generated/` — gitignored; regenerate with `sqlc generate -f internal/database/sqlc.yaml`

---

## Core Architecture Principle

**`internal/application/` must have zero `net/http` imports — ever.**

Everything HTTP-related lives exclusively in `cmd/`:
- `cmd/controllers/` — route registration, JSON decoding, calling handlers, JSON encoding
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

### `cmd/controllers/` — HTTP layer

Controllers receive `*http.ServeMux` in their constructor and self-register their routes. They:
1. Decode the JSON request body
2. Call the application handler
3. Write the response via `respond.NewResponse(w).JSON(status, result, err)`

Nothing from `net/http` leaks into `internal/application/`.

### `cmd/middleware/` — HTTP middleware

- `Recover(logger, next)` — wraps the entire mux globally. Catches panics, logs them, and writes a JSON error response.
- `Auth(jwtService)` — applied per-route inside controllers. Reads the `accessToken` cookie, validates it, stores `*shared.UserClaims` in context, or writes `401` directly and returns.

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

### `respond.NewResponse(w).JSON(status, result, err)`
The three-arg form is used everywhere. When `err != nil`, the `status` argument is ignored — the error drives the response. Never call `WriteHeader` or set headers directly in controllers.

```go
result, err := c.login.Handle(r.Context(), cmd)
respond.NewResponse(w).JSON(http.StatusOK, result, err)
```

### Decode errors (bad JSON body)
Controllers handle these directly — write the response and return. Do **not** panic:

```go
if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
    respond.NewResponse(w).JSON(http.StatusBadRequest, nil, apperror.NewBadRequest("invalid request body"))
    return
}
```

### Panics
`middleware.Recover` catches genuine unexpected panics (nil pointer, index out of range) and maps them to 500. It also handles `*apperror.AppError` panics → their own status code. Do not use panic as a normal control-flow mechanism for business errors.

---

## Go Naming Conventions

### Package names
Go package names must be **single lowercase words — no underscores, no hyphens**.

Feature directories can use underscores (e.g. `get_profile/`, `refresh_token/`), but the `package` declaration inside must drop them:

```go
// directory: internal/application/get_profile/
package getprofile  // ✅ correct

// directory: internal/application/refresh_token/
package refreshtoken  // ✅ correct
```

When importing a package whose directory name has underscores, use an alias to keep the call-site readable:

```go
import (
    getprofile  "auth/internal/application/get_profile"
    refreshtoken "auth/internal/application/refresh_token"
)
```

---

## Adding a New Feature Slice

For slice-specific requirements (business rules, JWT/cookie settings, password hash format, DB schema notes), read `SPEC.md` before starting.

1. Create `internal/application/<feature>/handler.go` — implement `Handler[Command, Result]`; wrap with `NewValidator` in `NewHandler`
2. Create `internal/application/<feature>/<feature>.sql` — sqlc-annotated SQL queries
3. Run `sqlc generate -f internal/database/sqlc.yaml`
4. Add the route to a controller in `cmd/controllers/` (or create a new one)
5. Register the controller in `cmd/main.go` (one line)
6. Add tests in `cmd/controller_tests/`

---

## Tests

All tests live in `cmd/controller_tests/` and are integration tests against a real PostgreSQL container (via `testcontainers-go`). A single container is shared across the package via `TestMain`.

`newTestHandler()` wires real controllers, real application handlers, a real JWT service (test secrets), and `Recover` middleware against the test DB — the full stack minus the email service.

Shared helpers:
- `registerUser(t, handler, email, password)` — registers via HTTP
- `loginUser(t, handler, email, password) string` — registers + logs in, returns the `accessToken` cookie value

---

## Key Files

| File | Why read it first |
|---|---|
| `cmd/respond/response.go` | `JSON(status, result, err)` — the one method used in every controller |
| `internal/application/app_validator.go` | `Handler[C,R]` interface and `Validator[C,R]` wrapper |
| `internal/application/apperror/errors.go` | `AppError` and `ValidationError` types |
| `cmd/middleware/auth.go` + `recover.go` | how HTTP middleware works |
| `cmd/controller_tests/main_test.go` | test container setup and `newTestHandler()` |
| `internal/application/register/handler.go` | reference implementation of a full feature slice |
