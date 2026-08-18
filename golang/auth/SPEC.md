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

### Architecture

Four layers, dependency flows strictly inward:

```
cmd/main.go → cmd/middleware/ → cmd/controllers/ → internal/application/
```

- `cmd/main.go` — thin entry point: loads config, opens DB, creates mux, wraps with `middleware.Recover`, starts server.
- `cmd/middleware/` — `Recover` middleware wraps the mux. Catches any panic and maps it to a JSON HTTP response: `*apperror.AppError` → status from `Code`, `*apperror.ValidationErrors` → 422, anything else → 500. This is the single authoritative place for error-to-status mapping.
- `cmd/controllers/` — HTTP layer. Each controller constructor receives `*http.ServeMux` and self-registers its routes. Controllers own JSON decoding and encoding. They contain no `if err != nil` checks — failures are signalled by panic. Nothing from `net/http` leaks into `internal/application/`.
- `internal/application/` — pure business logic. Handlers receive plain Go types and return plain Go types (no error return). On failure they panic with a typed error; the middleware catches it. No `net/http` imports anywhere in this layer.
- No repository layer. Handlers own all their DB logic directly.
- No mediator bus. Controllers call application handlers directly.
- No event system. Handlers that send email call the service directly in a fire-and-forget goroutine using `context.WithoutCancel`. RabbitMQ-based events are deferred to a future phase.

### Vertical Slice Delivery

Each slice is an independent unit of work delivered in sequence:

| # | Slice | Type |
|---|-------|------|
| 0 | Init project, Dockerfile, `/health` endpoint | Setup |
| 1 | Register | Command |
| 2 | Login | Command |
| 3 | RefreshToken | Command |
| 4 | Logout | Command |
| 5 | ConfirmEmail | Command |
| 6 | ResendEmailConfirmation | Command |
| 7 | ForgotPassword | Command |
| 8 | ResetPassword | Command |
| 9 | ChangePassword | Command |
| 10 | ChangeEmail | Command |
| 11 | ConfirmEmailChange | Command |
| 12 | GetProfile | Query |
| 13 | GetUserLoginHistories | Query |

### Database

- PostgreSQL via `database/sql` + `github.com/lib/pq`. No ORM.
- SQL queries are written by hand in `.sql` files annotated for sqlc. sqlc generates type-safe Go code into `internal/database/generated/`. Generated files are **gitignored** and must be regenerated with `sqlc generate -f internal/database/sqlc.yaml` before building. `sqlc` itself is not a runtime dependency.
- Each feature's SQL file lives alongside its handler: `login/login.sql` next to `login/handler.go`. sqlc scans all `.sql` files under `internal/application/` via the glob `"../application/**/*.sql"` configured in `internal/database/sqlc.yaml`. No manual listing of feature directories is needed — adding a new `<feature>/<feature>.sql` is enough.
- `internal/database/context.go` opens the connection, pings, and returns `*generated.Queries` (aliased as `dbsqlc` at import sites). Handlers receive `*dbsqlc.Queries` directly — no handler touches `*sql.DB`.
- Schema snapshots in `internal/database/schema/` define the table shape for sqlc type inference. They are never applied to the database.
- `UserLoginHistories` has a `RememberMe` column that the Login handler must write.

### Secrets and Configuration

- Config is loaded from a single JSON file at `CONFIG_PATH` env var (defaults to `credentials/appsettings.json`). Unknown keys are silently ignored.
- A dedicated `TOKEN_SECRET` is read exclusively from an environment variable. It is never written to `appsettings.json`. It is used only to HMAC-sign stateless email confirmation and password reset tokens.
- `TOKEN_SECRET` is separate from `Jwt.AccessTokenSecretKey` and `Jwt.RefreshTokenSecretKey`.

### JWT and Tokens

- `shared.JwtProvider` interface has three methods: `GenerateTokens`, `ValidateRefreshToken`, `ValidateAccessToken`.
- JWT claim shape matches the .NET implementation exactly: `id`, `emailCus`, `firstName`, `lastName`, `emailConfirmed`, `type`, `rte` (refresh token expiry, access token only).
- Access token expiry: 5 minutes (always).
- Refresh token expiry: 5 hours (default) or 30 days (when `RememberMe: true`), matching the .NET `JwtProvider`.
- Stateless HMAC-SHA256 tokens (signed with `TOKEN_SECRET`) are used for email confirmation, password reset, and email change confirmation. No token table in the database.

### Password Hashing

- PBKDF2-HMAC-SHA256 in ASP.NET Identity V3 format (10 000 iterations, 16-byte salt, 32-byte key, 61-byte layout). This format is required for interoperability with the .NET service — both apps share the `Users` table.

### Cookies

- `HttpOnly: true`, `Secure: !isDevelopment`, `SameSite: Strict` (frontend and API are same-origin).
- `isDevelopment` is derived from `APP_ENV == "Development"` and passed as a `bool` to `NewAuthHandler` at wiring time.

### HTTP

- `net/http` stdlib `ServeMux` (Go 1.22+) with method-prefixed patterns. No third-party router.
- Each controller in `cmd/controllers/` receives `*http.ServeMux` in its constructor and self-registers its routes. `main.go` calls one constructor per controller.
- All JSON responses are written via `respond.NewResponse(w).OK(v)` or `respond.NewResponse(w).JSON(status, v)` (`cmd/respond/response.go`). Controllers never set headers or call `WriteHeader` directly. Error responses are never written by controllers — failures are signalled by panic and handled centrally by `middleware.Recover`.
- `X-Forwarded-For` is trusted unconditionally for client IP extraction (acceptable given the controlled network topology).

### `/health` Endpoint

- Returns `{"app_env": "<APP_ENV>", "version": "<VERSION>"}` as JSON.
- `VERSION` is passed in via a `VERSION` environment variable (set at build time in the Dockerfile `ARG VERSION`).
- No authentication required.

### Validation

- `github.com/go-playground/validator/v10` with struct tags. Custom `password_strength` validator registered once on a shared `*validator.Validate` instance.
- Validator uses JSON field names (via `RegisterTagNameFunc`) so error field names match the JSON request body.
- Validation errors returned as `{"errors": [{"name": "fieldName", "errors": ["rule1"]}]}` with HTTP 422.

### Error Handling

- Application handlers signal failures by **panicking** with a typed error — never by returning an error value.
- `apperror.AppError` for typed HTTP errors (400, 401, 404, 409). Panic with `apperror.NewUnauthorized(...)`, `apperror.NewNotFound(...)`, etc.
- `apperror.ValidationErrors` for field-level validation failures (422). Panic with `apperror.NewValidationErrors(ve)`.
- Any other panic value (unexpected errors, genuine bugs) → 500 with `{"message": "Internal Server Error"}` — no internals leaked.
- The `Recover` middleware in `cmd/middleware/recover.go` is the only place that converts these panics to HTTP responses.

### Middleware

- `cmd/middleware/recover.go` — `Recover(next http.Handler) http.Handler`. Wraps the entire mux in `cmd/main.go`. Recovers any panic and writes a JSON error response. This is the only middleware applied globally.
- Auth middleware reads `accessToken` cookie, validates the JWT, and stores the user profile in context. Panics with `apperror.NewUnauthorized(...)` on failure so the `Recover` middleware handles the response. Applied per-route inside the relevant controller.

### Dependency Injection

Manual constructor injection. Controller constructors receive dependencies (DB, config values) directly — no DI framework. `main.go` is the only wiring point.

### Email

- Direct calls to `port.EmailService` (Mailjet) from within handlers, in a fire-and-forget goroutine with `context.WithoutCancel`. Email failures are logged but do not fail the HTTP response. A resend endpoint exists as a fallback for lost emails.

## Testing Decisions

- A good test exercises the handler's external behavior (inputs in, outputs out) without asserting on internal implementation details like which DB method was called or in what order.
- **Unit tests** for all application handlers — call handler methods directly with plain Go types. No HTTP setup needed because application handlers have no `net/http` dependency. These live alongside their handler in `internal/application/<feature>/`.
- **HTTP end-to-end tests** for all routes using `net/http/httptest`. These live in `cmd/controllers/` and wire real controllers against mock application handlers.
- Integration tests (testcontainers-go + real PostgreSQL) are deferred until Go owns at least one database table.
- No test is written for the current slice — the test structure is established in slice 0 and filled in as each slice is built.

## Out of Scope

- Event bus / RabbitMQ integration (future phase).
- Repository layer abstraction.
- Integration tests (deferred — Go owns no DB tables yet).
- DB migrations (deferred — all tables owned by .NET).
- Trusted-proxy IP validation for `X-Forwarded-For`.
- Any frontend changes.

## Further Notes

- The Go service and .NET service may run simultaneously against the same database. The PBKDF2 hash format, JWT claim names, and cookie names must be identical between the two implementations to avoid breaking existing sessions.
- `cmd/main.go` panics on bad config (missing/malformed `appsettings.json`). This is intentional — it is idiomatic Go for a fatal startup precondition.
- `cmd/controllers/` has no `net/http` restriction on it — only `internal/application/` must remain HTTP-free.
