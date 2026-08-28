# Go Auth Service — Spec

## Problem Statement

The .NET Auth module handles all authentication for QN.Expenditure (register, login, session management, password and email changes). The team wants a parallel Go implementation that shares the same SQL Server database and is fully compatible with existing password hashes, JWT tokens, and session records. This enables a future migration path away from .NET without disrupting existing users or data.

## Solution

Build `golang/auth` — a standalone Go HTTP service that re-implements all 13 auth use cases (11 commands, 2 queries). It runs alongside the .NET service against the same database, using the same PBKDF2-HMAC-SHA256 password format, the same JWT claim shape, and the same cookie strategy. The service is built as vertical slices: one slice per command/query, each delivered as a self-contained unit of work with its own SQL, handler, and HTTP route.

## User Stories

1. As a developer, I want a bootstrapped Go project with a Dockerfile and a `/health` endpoint that returns `APP_ENV` and `VERSION`, so that I can verify the service wires up and deploys correctly before adding any auth logic.
2. As a new user, I want to register with my email, password, first name, and last name, so that I can create an account.
3. As a registered user, I want to receive an email confirmation link after registering, so that my account can be activated.
4. As a registered user, I want to log in with my email and password, so that I can access authenticated features.
5. As a registered user, I want to choose "remember me" at login, so that my session persists for 30 days instead of expiring in 5 hours.
6. As a logged-in user, I want my access and refresh tokens set as HttpOnly cookies, so that they are not accessible to JavaScript on the client.
7. As a logged-in user, I want to refresh my session using my refresh token cookie, so that I stay logged in without re-entering my credentials.
8. As a logged-in user, I want to log out, so that my session is invalidated and my cookies are cleared.
9. As a logged-in user with an unconfirmed email, I want to confirm my email via the link sent to me, so that I can access all features.
10. As a logged-in user, I want to resend the email confirmation, so that I can recover if the original email was lost.
11. As a logged-in user, I want to change my password, so that I can update my credentials.
12. As a logged-in user, I want to request a password reset email, so that I can recover access if I forget my password.
13. As a logged-in user, I want to reset my password via a secure time-limited token, so that I can regain access to my account.
14. As a logged-in user, I want to change my email address, so that I can keep my account details up to date.
15. As a logged-in user, I want to confirm my email address change via a link, so that the change is verified before it takes effect.
16. As a logged-in user, I want to view my profile (name, email, confirmation status), so that I can see my account details.
17. As a logged-in user, I want to view my login history (IP, user agent, timestamp), so that I can audit which devices have accessed my account.
18. As a client application, I want a `GET /api/auth/check` endpoint, so that I can verify the current user's session without re-parsing the JWT on the client.
19. As a developer, I want all validation errors returned as a structured JSON body with field names and failed rule names, so that the frontend can display per-field error messages.
20. As a developer, I want all business errors returned with an appropriate HTTP status code and a `{"message": "..."}` body, so that the frontend has consistent error handling.

## Implementation Decisions

> Architecture, error handling, middleware, HTTP patterns, and testing decisions are documented in `CLAUDE.md` (the primary operational guide for this codebase).

### Architecture summary

Four layers, dependency flows strictly inward:

```
cmd/main.go → cmd/middleware/ → cmd/controllers/ → internal/application/
```

- `internal/application/` — **zero `net/http` imports**. Pure business logic. Handlers return `(R, error)`.
- `cmd/` — all HTTP concerns: routing, decoding, encoding, middleware, cookies.
- No repository layer, no mediator bus. Handlers own DB logic directly. Email is fire-and-forget with `context.WithoutCancel`.

### Slices

Each slice is an independent unit of work delivered in sequence:

| # | Slice | Type | Status |
|---|-------|------|--------|
| 0 | Init project, Dockerfile, `/health` endpoint | Setup | ✅ Done |
| 1 | Register | Command | ✅ Done |
| 2 | Login | Command | ✅ Done |
| 3 | RefreshToken | Command | — |
| 4 | Logout | Command | — |
| 5 | ConfirmEmail | Command | — |
| 6 | ResendEmailConfirmation | Command | — |
| 7 | ForgotPassword | Command | — |
| 8 | ResetPassword | Command | — |
| 9 | ChangePassword | Command | — |
| 10 | ChangeEmail | Command | — |
| 11 | ConfirmEmailChange | Command | — |
| 12 | GetProfile | Query | ✅ Done |
| 13 | GetUserLoginHistories | Query | — |

### Database

- PostgreSQL via `database/sql` + `github.com/lib/pq`. No ORM.
- SQL queries are written by hand in `.sql` files annotated for sqlc. sqlc generates type-safe Go code into `internal/database/generated/`. Generated files are **gitignored** and must be regenerated with `sqlc generate -f internal/database/sqlc.yaml` before building. `sqlc` itself is not a runtime dependency.
- Each feature's SQL file lives alongside its handler: `login/login.sql` next to `login/handler.go`. sqlc scans all `.sql` files under `internal/application/` via the glob `"../application/**/*.sql"` configured in `internal/database/sqlc.yaml`. No manual listing of feature directories is needed — adding a new `<feature>/<feature>.sql` is enough.
- `internal/database/context.go` opens the connection, pings, and returns `(*sql.DB, *generated.Queries, error)`. The `*sql.DB` is retained by `main.go` for `defer db.Close()`. Handlers receive `*generated.Queries` directly — no handler touches `*sql.DB`.
- Schema snapshots in `internal/database/schema/` define the table shape for sqlc type inference. They are never applied to the database.
- `UserLoginHistories` has a `RememberMe` column that the Login handler must write.

### Secrets and Configuration

- Config is loaded from a single JSON file at `CONFIG_PATH` env var (defaults to `credentials/appsettings.json`). Unknown keys are silently ignored.
- A dedicated `TOKEN_SECRET` is read exclusively from an environment variable. It is never written to `appsettings.json`. It is used only to HMAC-sign stateless email confirmation and password reset tokens.
- `TOKEN_SECRET` is separate from `Jwt.AccessTokenSecretKey` and `Jwt.RefreshTokenSecretKey`.

### JWT and Tokens

- `shared.JwtService` interface has three methods: `GenerateTokens`, `ValidateRefreshToken`, `ValidateAccessToken`.
- JWT claim shape matches the .NET implementation exactly: `id`, `email`, `firstName`, `lastName`, `emailConfirmed`, `type`, `rte` (refresh token expiry, access token only).
- Access token expiry: 5 minutes (always).
- Refresh token expiry: 5 hours (default) or 30 days (when `RememberMe: true`), matching the .NET `JwtProvider`.
- Stateless HMAC-SHA256 tokens (signed with `TOKEN_SECRET`) are used for email confirmation, password reset, and email change confirmation. No token table in the database.

### Password Hashing

- PBKDF2-HMAC-SHA256 in ASP.NET Identity V3 format (10 000 iterations, 16-byte salt, 32-byte key, 61-byte layout). This format is required for interoperability with the .NET service — both apps share the `Users` table.

### Cookies

- `HttpOnly: true`, `Secure: !isDevelopment`, `SameSite: Strict` (frontend and API are same-origin).
- `isDevelopment` is derived from `APP_ENV == "Development"` and passed as a `bool` to `NewAuthController` at wiring time.

### HTTP

- `net/http` stdlib `ServeMux` (Go 1.22+) with method-prefixed patterns. No third-party router.
- Each controller in `cmd/controllers/` receives `*http.ServeMux` in its constructor and self-registers its routes. `main.go` calls one constructor per controller.
- All JSON responses are written via `respond.NewResponse(w).JSON(status, result, err)` or `respond.NewResponse(w).OK(v)` (`cmd/respond/response.go`). When `err != nil`, `JSON` writes the error response instead of the success body — `*apperror.AppError` maps to its own status code and `{"message":"..."}`, `*apperror.ValidationError` maps to 422 with `[{"name":"field","errors":["msg"]}]`, anything else maps to 500. Controllers never call `WriteHeader` directly.
- `X-Forwarded-For` is trusted unconditionally for client IP extraction (acceptable given the controlled network topology).

### `/health` Endpoint

- Returns `{"app_env": "<APP_ENV>", "version": "<VERSION>"}` as JSON.
- `VERSION` is passed in via a `VERSION` environment variable (set at build time in the Dockerfile `ARG VERSION`).
- No authentication required.

### Validation

- `github.com/go-playground/validator/v10` with struct tags. Custom validators (e.g. `password_strength`) registered via `NewValidator` configure functions.
- Validator uses JSON field names (via `RegisterTagNameFunc`) so error field names match the JSON request body.
- Validation errors returned as a JSON array `[{"name":"fieldName","errors":["message"]}]` with HTTP 422. Fields are sorted alphabetically.

### Error Handling

- Application handlers return `(R, error)`. Business rule failures are returned as `*apperror.AppError`; validation failures as `*apperror.ValidationError`.
- `respond.NewResponse(w).JSON(status, result, err)` inspects `err` and writes the correct HTTP response:
  - `*apperror.AppError` → `e.Code` status + `{"message": e.Message}`
  - `*apperror.ValidationError` → 422 + `[{"name":"...","errors":["..."]}]`
  - any other error → 500 + `{"message": "Internal Server Error"}`
  - `nil` → `status` + encoded `result`
- `middleware.Recover` catches panics (unexpected bugs, genuine runtime errors) and maps `*apperror.AppError` to its status code, everything else to 500. Panics are logged via `slog`.
- Controllers write decode errors (bad JSON body) directly via `respond.NewResponse(w).JSON(http.StatusBadRequest, nil, apperror.NewBadRequest(...))` and return — they do not panic for decode failures.

### Middleware

- `cmd/middleware/recover.go` — `Recover(logger *slog.Logger, next http.Handler) http.Handler`. Wraps the entire mux in `cmd/main.go`. Recovers any panic, logs it, and writes a JSON error response.
- `cmd/middleware/auth.go` — `Auth(jwtService) func(http.HandlerFunc) http.HandlerFunc`. Applied per-route inside controllers. Reads the `accessToken` cookie, validates it via `jwtService.ValidateAccessToken`, stores `*shared.UserClaims` in context, or writes a `401` response directly and returns.

### Dependency Injection

Manual constructor injection. Controller constructors receive dependencies (DB, config values) directly — no DI framework. `main.go` is the only wiring point.

### Email

- Direct calls to `shared.EmailService` (Mailjet) from within handlers, in a fire-and-forget goroutine with `context.WithoutCancel`. Email failures are logged but do not fail the HTTP response. A resend endpoint exists as a fallback for lost emails.

## Testing Decisions

- Tests exercise the handler's external behavior (HTTP inputs and outputs) without asserting on internal implementation details.
- **Integration tests** using `testcontainers-go` with a real PostgreSQL container. Tests live in `cmd/controller_tests/`. A single container is shared across all tests in the package via `TestMain`.
- `newTestHandler()` wires real controllers, real application handlers, a real JWT service (with test secrets), and the `Recover` middleware against the test DB — the full production stack minus the email service.
- No unit tests for application handlers (they are covered by the integration tests end-to-end).
- Test helpers (`registerUser`, `loginUser`) reduce boilerplate for common setup steps.

## Out of Scope

- Event bus / RabbitMQ integration (future phase).
- Repository layer abstraction.
- Trusted-proxy IP validation for `X-Forwarded-For`.
- Any frontend changes.

## Further Notes

- The Go service and .NET service may run simultaneously against the same database. The PBKDF2 hash format, JWT claim names, and cookie names must be identical between the two implementations to avoid breaking existing sessions.
- `cmd/main.go` exits with code 1 on bad config or DB connection failure.
- `cmd/controllers/` has no `net/http` restriction on it — only `internal/application/` must remain HTTP-free.
