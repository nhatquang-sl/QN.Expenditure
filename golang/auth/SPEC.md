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

- Four-layer Clean Architecture: `cmd/` (wiring), `internal/transport/http/` (presentation), `internal/infrastructure/` (external deps), `internal/application/` (use cases + DB contract).
- No repository layer. Each command/query handler receives `*sqlc.Queries` directly and owns all its DB logic in one place.
- No mediator bus. The HTTP handler calls each use case handler directly via its local `Handler` interface.
- No event system. Handlers that send email call the email service (or mailer) directly in a fire-and-forget goroutine using `context.WithoutCancel`. RabbitMQ-based events are deferred to a future phase.

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

- All tables (`Users`, `UserLoginHistories`) are owned by the .NET EF Core migrations. Go has no migrations of its own. The Go service connects to the same database and reads/writes directly.
- Schema snapshots in `internal/application/db/schema/` are derived from `AuthDbContextModelSnapshot.cs` and used only for sqlc type inference — never applied to the database.
- Schema snapshot synchronization with the .NET model snapshot is maintained by manual discipline.
- `UserLoginHistories` has a `RememberMe` (bit, not null) column that the Go Login handler must read and write.

### Secrets and Configuration

- Config is loaded from the shared `credentials/appsettings.json` and `credentials/appsettings.<APP_ENV>.json` (shallow merge, .NET layering convention). Unknown keys are silently ignored.
- A dedicated `TOKEN_SECRET` is read exclusively from an environment variable. It is never written to `appsettings.json`. It is used only to HMAC-sign stateless email confirmation and password reset tokens.
- `TOKEN_SECRET` is separate from `Jwt.AccessTokenSecretKey` and `Jwt.RefreshTokenSecretKey`.

### JWT and Tokens

- `port.JwtProvider` interface has three methods: `GenerateTokens`, `ValidateRefreshToken`, `ValidateAccessToken`.
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
- `AppHandlerFunc` custom type wraps handlers to propagate errors to a central `writeError` function.
- `X-Forwarded-For` is trusted unconditionally for client IP extraction (acceptable given the controlled network topology).
- `ValidatingDecorator[C, R]` wraps every command/query handler at wiring time in `cmd/main.go`. Forgetting to wrap a handler is caught by tests, not the compiler.

### `/health` Endpoint

- Returns `{"app_env": "<APP_ENV>", "version": "<VERSION>"}` as JSON.
- `VERSION` is passed in via a `VERSION` environment variable (set at build time in the Dockerfile `ARG VERSION`).
- No authentication required.

### Validation

- `github.com/go-playground/validator/v10` with struct tags. Custom `password_strength` validator registered once on a shared `*validator.Validate` instance.
- Validator uses JSON field names (via `RegisterTagNameFunc`) so error field names match the JSON request body.
- Validation errors returned as `{"errors": [{"name": "fieldName", "errors": ["rule1"]}]}` with HTTP 422.

### Error Handling

- `apperror.AppError` for typed HTTP errors (400, 404, 409, 422).
- `apperror.ValidationErrors` for field-level validation failures (422).
- Internal errors return 500 with `{"message": "Internal Server Error"}` (no leak of internals).

### Middleware

- `middleware.Auth` — reads `accessToken` cookie, calls `jwtProvider.ValidateAccessToken`, stores `*dto.UserProfile` in context. Returns 401 JSON on failure.
- `middleware.Performance` — logs requests slower than 500ms at WARN level.
- `middleware.Recovery` — panic recovery, returns 500.

### Dependency Injection

Manual constructor injection in `cmd/main.go`. No DI framework.

### Email

- Direct calls to `port.EmailService` (Mailjet) from within handlers, in a fire-and-forget goroutine with `context.WithoutCancel`. Email failures are logged but do not fail the HTTP response. A resend endpoint exists as a fallback for lost emails.

## Testing Decisions

- A good test exercises the handler's external behavior (inputs in, outputs out) without asserting on internal implementation details like which DB method was called or in what order.
- **Unit tests** for all command and query handlers using `go-sqlmock` to mock the DB driver. These live in `internal/application/command/<name>/` and `internal/application/query/<name>/`.
- **HTTP end-to-end tests** for all routes using `net/http/httptest`. These live in `internal/transport/http/` and wire real handlers against mock infrastructure.
- Integration tests (testcontainers-go + real SQL Server) are deferred until Go owns at least one database table with a goose migration.
- No test is written for the current slice — the test structure is established in slice 0 and filled in as each slice is built.

## Out of Scope

- Event bus / RabbitMQ integration (future phase).
- Repository layer abstraction.
- Integration tests (deferred — Go owns no DB tables yet).
- Goose migrations (deferred — all tables owned by .NET).
- Trusted-proxy IP validation for `X-Forwarded-For`.
- Schema snapshot CI sync check (manual discipline for now).
- Any frontend changes.

## Further Notes

- The `UserLoginHistories` schema snapshot must include the `RememberMe bit NOT NULL` column added in `.NET` migration `20260521111350_AddRememberMeToUserLoginHistory`. The Go Login handler stores this value alongside the tokens.
- The Go service and .NET service may run simultaneously against the same database. The PBKDF2 hash format, JWT claim names, and cookie names must be identical between the two implementations to avoid breaking existing sessions.
- `cmd/main.go` panics on bad config (missing/malformed `appsettings.json`). This is intentional — it is idiomatic Go for a fatal startup precondition.
