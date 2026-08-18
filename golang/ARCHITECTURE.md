# Go Architecture: Auth Module

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Technology Choices](#2-technology-choices)
3. [Project Folder Structure](#3-project-folder-structure)
4. [Layer-by-Layer Design](#4-layer-by-layer-design)
5. [Dependency Injection](#5-dependency-injection)
6. [Key Design Decisions](#6-key-design-decisions)

---

## 1. Architecture Overview

The Go Auth module uses a two-layer structure. The dependency rule flows strictly inward — `cmd/main.go` depends on `internal/application/`, never the reverse.

```
┌──────────────────────────────────────────────────────┐
│              cmd/main.go                             │  <- Entry point: wires mux, starts server
├──────────────────────────────────────────────────────┤
│         cmd/middleware/                              │  <- Recover: catches panics → HTTP responses
├──────────────────────────────────────────────────────┤
│         cmd/controllers/                             │  <- HTTP layer: decode, call, encode
├──────────────────────────────────────────────────────┤
│         internal/application/                        │  <- Pure logic, no net/http
├──────────────────────────────────────────────────────┤
│         internal/config/  internal/database/         │  <- Config + DB
└──────────────────────────────────────────────────────┘
```

**Core Principle**: Application handlers in `internal/application/` contain pure business logic with **no dependency on `net/http`**. They receive plain Go types and return plain Go types (no error return). When a business rule or validation fails, they **panic with a typed error** (`*apperror.AppError` or `*apperror.ValidationErrors`). The `Recover` middleware in `cmd/middleware/` intercepts every panic and maps it to the correct HTTP response — keeping error-to-status-code logic in one place. Controllers in `cmd/controllers/` own decoding and encoding, and register their own routes by receiving `*http.ServeMux` in their constructor.

**Key pattern** — the health handler is the canonical example:

```go
// internal/application/health/handler.go — no net/http
func (h *Handler) Handle() map[string]string {
    return map[string]string{"status": "healthy"}
}
```

```go
// cmd/controllers/health_controller.go — owns HTTP, self-registers routes
func NewHealthController(mux *http.ServeMux) {
    c := &HealthController{handler: &health.Handler{}}
    mux.HandleFunc("/health", c.handle)
}
```

```go
// cmd/main.go — one line per controller
controllers.NewHealthController(mux)
```

---

## 2. Technology Choices

| Concern | Choice | Rationale |
|---------|--------|-----------|
| HTTP | `net/http` (stdlib) | Zero dependencies; Go 1.22+ `ServeMux` handles method + path routing natively |
| Database Driver | `github.com/lib/pq` | PostgreSQL driver via standard `database/sql` |
| SQL Code Gen | `sqlc` (dev tool) | Generates type-safe Go from hand-written SQL; compile-time query safety, no runtime reflection. Generated files are committed; `sqlc` itself is not a runtime dependency |
| Config | Single JSON file (`credentials/appsettings.json`) | Read via `CONFIG_PATH` env var; no env-overlay complexity at this stage |
| Logging | `log/slog` (stdlib) | Structured logging, standard library since Go 1.21 |
| TLS | Optional — configured via `TLSCertPath`/`TLSKeyPath` in config | Server falls back to plain HTTP if not set |

---

## 3. Project Folder Structure

```
golang/
└── auth/
    ├── cmd/
    │   ├── main.go                         # Entry point: creates mux, wraps with middleware, starts server
    │   ├── respond/
    │   │   └── response.go                 # respond.Response — JSON(status, v) and OK(v) helpers
    │   ├── middleware/
    │   │   └── recover.go                  # Recover(next) — catches panics, maps to HTTP responses
    │   └── controllers/                    # HTTP layer — decode, call application handler, encode
    │       ├── health_controller.go        # NewHealthController(mux) — registers /health route
    │       └── auth_controller.go          # NewAuthController(mux, db, ...) — registers auth routes
    │
    ├── internal/
    │   ├── application/                    # Application layer — pure logic, no net/http imports
    │   │   ├── apperror/
    │   │   │   └── errors.go               # AppError, ValidationErrors + constructors
    │   │   ├── shared/
    │   │   │   └── jwt.go                  # JwtProvider interface + TokenPair, UserClaims types
    │   │   ├── app_handler.go              # application.Handler: aggregates all feature handlers
    │   │   ├── health/
    │   │   │   └── handler.go              # health.Handler: Handle() map[string]string
    │   │   └── login/
    │   │       ├── handler.go              # Login command handler — no net/http
    │   │       └── login.sql               # sqlc: GetUserByNormalizedEmail, CreateLoginHistory
    │   │
    │   ├── config/
    │   │   └── config.go                   # Config struct (with JwtConfig named type) + LoadJSONConfig()
    │   ├── database/
    │   │   ├── sqlc.yaml                   # sqlc generator configuration
    │   │   ├── schema/                     # Table snapshots for sqlc type inference — never applied to DB
    │   │   │   └── schema.sql
    │   │   ├── generated/                  # Generated by sqlc — NEVER hand-edited; gitignored
    │   │   │   ├── db.go                   # DBTX interface + Queries struct
    │   │   │   ├── models.go               # Plain Go structs mirroring table columns
    │   │   │   └── *.sql.go                # Generated query methods per feature
    │   │   └── context.go                  # ConnectDB() — opens connection, returns *generated.Queries
    │   └── jwt/
    │       └── provider.go                 # JwtProvider implementation (golang-jwt/jwt/v5)
    │
    ├── go.mod
    ├── go.sum
    ├── Makefile
    ├── Dockerfile
    └── .air.toml                           # Live-reload config for development
```

---

## 4. Layer-by-Layer Design

### 4.1 Application Layer (`internal/application/`)

**Rule:** Nothing in this layer imports `net/http`. Handlers receive and return plain Go types only.

Each feature gets its own sub-package with a `Handler` struct. The top-level `application.Handler` aggregates all feature handlers via named fields.

```go
// internal/application/app_handler.go
package application

import "auth/internal/application/health"

type Handler struct {
    Health *health.Handler
}

func NewHandler() *Handler {
    return &Handler{}
}
```

```go
// internal/application/health/handler.go
package health

type Handler struct{}

func (h *Handler) Handle() map[string]string {
    return map[string]string{"status": "healthy"}
}
```

Handlers receive `*generated.Queries` (the sqlc-generated query struct, aliased as `dbsqlc`) and own all their DB logic directly. SQL query files live alongside the handler that uses them — `login/login.sql` next to `login/handler.go`. sqlc scans the entire `internal/application/` tree via a glob and generates type-safe methods into `internal/database/generated/`.

```sql
-- internal/application/login/login.sql

-- name: GetUserByNormalizedEmail :one
SELECT "Id", "Email", "FirstName", "LastName", "EmailConfirmed", "PasswordHash"
FROM "Users"
WHERE "NormalizedEmail" = $1;

-- name: CreateLoginHistory :exec
INSERT INTO "UserLoginHistories" ("UserId", "IpAddress", "UserAgent", "AccessToken", "RefreshToken", "CreatedAt", "RememberMe")
VALUES ($1, $2, $3, $4, $5, $6, $7);
```

```go
// internal/application/login/handler.go
type Handler struct {
    db          *dbsqlc.Queries          // dbsqlc = "auth/internal/database/generated"
    jwtProvider shared.JwtProvider
    logger      *slog.Logger
    validate    *validator.Validate
}

// Handle returns Result directly — panics with *apperror.AppError or
// *apperror.ValidationErrors on failure; the Recover middleware catches them.
func (h *Handler) Handle(ctx context.Context, cmd Command) Result {
    if err := h.validate.Struct(cmd); err != nil {
        // ...
        panic(apperror.NewValidationErrors(ve))
    }
    // ...
    panic(apperror.NewUnauthorized("invalid credentials"))
    // ...
    return Result{...}
}
```

When adding a new feature (e.g. `register`):
1. Create `internal/application/register/handler.go` — pure logic, no HTTP
2. Create `internal/application/register/register.sql` — sqlc-annotated SQL queries
3. Run `sqlc generate -f internal/database/sqlc.yaml` to regenerate `internal/database/generated/`
4. Create `cmd/controllers/register_controller.go` (or add routes to an existing controller)

### 4.2 Middleware (`cmd/middleware/`)

`recover.go` wraps the entire mux. Any panic that propagates out of a controller or handler is caught here and converted to a JSON HTTP response. The type switch is the single authoritative mapping from error type to status code.

```go
// cmd/middleware/recover.go
func Recover(next http.Handler) http.Handler {
    return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
        defer func() {
            if rec := recover(); rec != nil {
                writeError(w, rec)
            }
        }()
        next.ServeHTTP(w, r)
    })
}

func writeError(w http.ResponseWriter, rec any) {
    res := respond.NewResponse(w)
    switch e := rec.(type) {
    case *apperror.AppError:
        res.JSON(e.Code, map[string]string{"message": e.Message})
    case *apperror.ValidationErrors:
        res.JSON(http.StatusUnprocessableEntity, e)
    default:
        res.JSON(http.StatusInternalServerError, map[string]string{"message": "Internal Server Error"})
    }
}
```

Applied once in `main.go`:

```go
srv := &http.Server{Addr: serverAddr, Handler: middleware.Recover(mux)}
```

### 4.3 Controller Layer (`cmd/controllers/`)

Controllers own decoding requests and encoding responses. **Nothing from `net/http` leaks into `internal/application/`.**

The constructor receives `*http.ServeMux` and registers routes directly — no separate `Register` method needed. Because the `Recover` middleware handles all error responses, controllers contain no `if err != nil` checks — they simply call the handler and encode the result.

```go
// cmd/controllers/health_controller.go
func NewHealthController(mux *http.ServeMux) {
    c := &HealthController{handler: &health.Handler{}}
    mux.HandleFunc("/health", c.handle)
}

func (c *HealthController) handle(w http.ResponseWriter, r *http.Request) {
    respond.NewResponse(w).OK(c.handler.Handle())
}
```

```go
// cmd/controllers/auth_controller.go — no error checks needed
func (c *AuthController) handleLogin(w http.ResponseWriter, r *http.Request) {
    var cmd login.Command
    if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
        panic(apperror.NewBadRequest("invalid request body"))
    }
    result := c.login.Handle(r.Context(), cmd)
    respond.NewResponse(w).OK(map[string]any{
        "id":             result.Id,
        "email":          result.Email,
        "firstName":      result.FirstName,
        "lastName":       result.LastName,
        "emailConfirmed": result.EmailConfirmed,
    })
}
```

`main.go` registration stays a single line per controller:

```go
controllers.NewHealthController(mux)
controllers.NewAuthController(mux, queries, jwtProvider, logger, isDev)
```

#### sqlc Configuration (`internal/database/sqlc.yaml`)

```yaml
version: "2"
sql:
  - engine: "postgresql"
    queries:
      - "../application/**/*.sql"   # glob: picks up every .sql in any feature subfolder
    schema:
      - "schema/"                   # table snapshots — for sqlc type inference only, never applied to DB
    gen:
      go:
        package: "generated"
        out: "generated"
        emit_pointers_for_null_types: true
        emit_json_tags: false
        emit_db_tags: false
```

Run `sqlc generate -f internal/database/sqlc.yaml` after adding or changing any `.sql` file. The generated files in `internal/database/generated/` are **gitignored** — regenerate locally or in CI before building. Never hand-edit them.

### 4.4 Config (`internal/config/config.go`)

Single JSON file loaded from `CONFIG_PATH` env var (defaults to `credentials/appsettings.json`). TLS paths default to local dev certs if not set in the JSON.

```go
type JwtConfig struct {
    Issuer                string
    Audience              string
    AccessTokenSecretKey  string
    RefreshTokenSecretKey string
}

type Config struct {
    ConnectionStrings struct {
        AuthConnection string
        CexConnection  string
    }
    Application struct {
        Version  string
        Endpoint string
    }
    Jwt          JwtConfig
    TLSCertPath  string
    TLSKeyPath   string
    GoServerPort int
}
```

### 4.5 Database (`internal/database/`)

`context.go` opens a PostgreSQL connection, verifies it with `Ping`, and returns `*generated.Queries` (the sqlc-generated query struct) ready for use by handlers.

```go
func ConnectDB(connectionString string) *generated.Queries {
    conn, err := sql.Open("postgres", connectionString)
    // ...ping...
    return generated.New(conn)
}
```

`main.go` receives `*generated.Queries` (imported as `dbsqlc`) and passes it to controller constructors, which forward it to handlers. No handler ever touches `*sql.DB` directly.

The `schema/` subfolder holds table-shape snapshots used exclusively by sqlc for type inference — they are never applied to the database. The `generated/` subfolder is gitignored; it must be regenerated with `sqlc generate -f internal/database/sqlc.yaml` before building.

### 4.6 Entry Point (`cmd/main.go`)

`main.go` is intentionally thin:
- Parses config
- Opens DB connection
- Creates the mux and passes it to each controller constructor
- Starts the server

```go
func main() {
    logger := slog.New(slog.NewTextHandler(os.Stdout, nil))
    cfg := config.LoadJSONConfig()

    queries := database.ConnectDB("...")  // returns *generated.Queries

    mux := http.NewServeMux()

    controllers.NewHealthController(mux)
    controllers.NewAuthController(mux, queries, jwtProvider, logger, isDev)

    srv := &http.Server{Addr: fmt.Sprintf(":%d", cfg.GoServerPort), Handler: middleware.Recover(mux)}

    if cfg.TLSCertPath != "" && cfg.TLSKeyPath != "" {
        srv.ListenAndServeTLS(cfg.TLSCertPath, cfg.TLSKeyPath)
    } else {
        srv.ListenAndServe()
    }
}
```

---

## 5. Dependency Injection

Manual wiring in `cmd/main.go` — explicit, zero magic. Controller constructors receive all dependencies they need. Handlers are constructed inside the controller.

```go
// Pattern for a feature controller that needs DB + JWT:
func NewAuthController(mux *http.ServeMux, db *dbsqlc.Queries, jwtProvider shared.JwtProvider, logger *slog.Logger, isDev bool) {
    c := &AuthController{
        login: login.NewHandler(db, jwtProvider, logger),
        isDev: isDev,
    }
    mux.HandleFunc("POST /api/auth/login", c.handleLogin)
}
```

---

## 6. Key Design Decisions

**Why are application handlers HTTP-free?**
Keeping `net/http` out of `internal/application/` means every handler is a plain Go function call — no request/response objects, no framework coupling. Unit tests call `handler.Handle()` directly; no `httptest` setup needed. The HTTP concern is fully contained in `cmd/controllers/`.

**Why do controllers self-register by receiving `*http.ServeMux`?**
Each controller constructor (`NewHealthController(mux)`) wires its own routes, so `main.go` never needs to know which paths a controller handles. Adding a new feature is one line in `main.go`. There is no separate `Register` method to call — construction and registration are a single step.

**Why `respond.Response` instead of writing to `http.ResponseWriter` directly?**
Every JSON response requires the same three steps: set `Content-Type`, call `WriteHeader`, then encode the body. Writing these inline in every controller method is repetitive and error-prone — it is easy to call `WriteHeader` after the body encoder has already flushed headers, causing a silent no-op. `respond.Response` (`cmd/respond/response.go`) centralises this sequence in two methods: `JSON(status, v)` and `OK(v)` (shorthand for status 200). Controllers and middleware use it consistently. Adding a new response shape (e.g., `Created`, `NoContent`) requires one change in one place.

```go
// cmd/respond/response.go
type Response struct{ w http.ResponseWriter }

func NewResponse(w http.ResponseWriter) Response { return Response{w: w} }

func (r Response) JSON(status int, v any) {
    r.w.Header().Set("Content-Type", "application/json")
    r.w.WriteHeader(status)
    json.NewEncoder(r.w).Encode(v)
}

func (r Response) OK(v any) { r.JSON(http.StatusOK, v) }
```

**Why panic instead of returning errors from application handlers?**
Application handlers panic with typed errors (`*apperror.AppError`, `*apperror.ValidationErrors`) rather than returning `(Result, error)`. This removes the `if err != nil { writeError(w, err); return }` boilerplate from every controller method and eliminates the risk of a double-write bug (writing the response body after headers were already sent). The `Recover` middleware is the single authoritative place that maps error types to HTTP status codes — adding a new error type means editing one `switch` case, not every controller. Panics from genuine bugs (nil pointer, index out of range) are caught by the same middleware and return a 500, preventing a goroutine crash from taking down the server.

**Why `net/http` instead of Gin or Echo?**
Go 1.22 added method-prefixed routing (`GET /health`) and path parameters (`{id}`) to `http.ServeMux`. No third-party router is needed for this use case. Stdlib middleware (`func(http.Handler) http.Handler`) is universally composable and testable.

**Why sqlc instead of an ORM or raw `database/sql`?**
sqlc generates type-safe Go code directly from hand-written SQL. Every query is visible, reviewed in PRs, and validated at code-generation time — no runtime reflection, no magic query building, no hidden N+1 risk. The generated structs in `internal/database/generated/` are ordinary Go types with no framework coupling. `lib/pq` is the underlying PostgreSQL driver; sqlc uses it via `database/sql`. Generated files are gitignored and regenerated in CI (`sqlc generate -f internal/database/sqlc.yaml`) before the build step.

**Why a single JSON config file?**
Simplicity. The binary reads one file at startup, controlled by `CONFIG_PATH`. Environment-specific values are handled at deployment time (different config files per environment), not at code level.
