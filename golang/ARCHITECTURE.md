# Go Clean Architecture: Auth Module

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Technology Choices](#2-technology-choices)
3. [Project Folder Structure](#3-project-folder-structure)
4. [Layer-by-Layer Design](#4-layer-by-layer-design)
5. [CQRS Adaptation in Go](#5-cqrs-adaptation-in-go)
6. [Dependency Injection](#6-dependency-injection)
7. [Error Handling](#7-error-handling)
8. [Middleware Pipeline](#8-middleware-pipeline)
9. [Testing Strategy](#9-testing-strategy)
10. [.NET vs Go Equivalents](#10-net-vs-go-equivalents)

---

## 1. Architecture Overview

The Go Auth module mirrors the four-layer Clean Architecture of the .NET `src/Auth` implementation. The dependency rule flows strictly inward — outer layers depend on inner layers, never the reverse.

```
┌──────────────────────────────────────────────────────┐
│              cmd/main.go                             │  <- Entry point & wiring
├──────────────────────────────────────────────────────┤
│         internal/transport/http/                     │  <- Presentation (net/http handlers)
├──────────────────────────────────────────────────────┤
│         internal/infrastructure/                     │  <- Infrastructure (DB connection, JWT, email)
├──────────────────────────────────────────────────────┤
│         internal/application/                        │  <- Application (use cases, CQRS)
└──────────────────────────────────────────────────────┘
```

**Core Principle**: Go is not OOP like C#. Rather than inheriting from base classes or using generic interface dispatch (MediatR), Go uses explicit function calls through interface types. Each use case handler is its own concrete struct. The "mediator" is simply the HTTP layer calling handlers directly through interfaces. This is idiomatic Go.

Each handler receives `*sqlc.Queries` directly and owns all its DB logic in one place. There is no repository layer — adding one would split a single use case's logic across two locations (handler + repository) with no benefit.

### Use Cases (mirrors `src/Auth` exactly)

| Type | Use Cases |
|------|-----------|
| Commands | Register, Login, Logout, RefreshToken, ConfirmEmail, ChangePassword, ChangeEmail, ConfirmEmailChange, ForgotPassword, ResetPassword, ResendEmailConfirmation |
| Queries | GetProfile, GetUserLoginHistories |
| Events | RegisterEvent, ForgotPasswordEvent, ChangeEmailEvent, ResendEmailConfirmationEvent |

---

## 2. Technology Choices

| Concern | Choice | Rationale |
|---------|--------|-----------|
| HTTP Framework | `net/http` (stdlib) | Zero dependencies; Go 1.22+ `ServeMux` supports method + path routing (`POST /api/auth/login`) and path parameters (`{id}`); middleware via `func(http.Handler) http.Handler` wrapping |
| SQL Code Gen | `sqlc` (dev tool) | Generates type-safe Go code from hand-written SQL; compile-time query safety, no runtime reflection, zero ORM magic — aligns with Go's explicit philosophy. Generated files are committed; `sqlc` itself is not a runtime dependency. |
| Database Driver | `github.com/microsoft/go-mssqldb` | Official Microsoft Go driver for SQL Server; pure Go, no cgo; supports named parameters (`@param`), `OUTPUT INSERTED.*`, and `OFFSET/FETCH` pagination |
| Schema Migrations | `pressly/goose` v3 | SQL-file-based migrations embedded via `go:embed`; applied programmatically at startup (`goose.RunContext`) — same behavior as `AutoMigrate` but deterministic, reviewable in PRs, and environment-agnostic |
| Dependency Injection | Manual constructor injection | Idiomatic Go; explicit, zero magic; readable in `main.go`; Wire can be adopted if the project grows |
| Validation | `github.com/go-playground/validator/v10` | Direct equivalent of FluentValidation; struct tags + custom `validate` functions for password rules |
| JWT | `github.com/golang-jwt/jwt/v5` | Direct equivalent of `System.IdentityModel.Tokens.Jwt`; HMAC-SHA256, custom claims, dual-secret support |
| Password Hashing | `golang.org/x/crypto/pbkdf2` + `crypto/sha256` | **Must match** ASP.NET Identity V3 PBKDF2-HMAC-SHA256 format — Go and .NET share the same `Users` table, so hashes produced by one app must be verifiable by the other. bcrypt is incompatible with existing hashes. |
| Email | `github.com/mailjet/mailjet-apiv3-go/v4` | Matches the Mailjet client already used in .NET notifications |
| Logging | `log/slog` (stdlib) | Standard library since Go 1.21; structured logging equivalent to `ILogTrace`; no extra dependency |
| Testing | `github.com/stretchr/testify` | `assert` + `require` + `mock` replaces xUnit + Shouldly + Moq |

---

## 3. Project Folder Structure

```
golang/
└── auth/
    ├── cmd/
    │   └── main.go                                 # Entry point: wires all dependencies, starts server
    │
    ├── internal/
    │   ├── application/                            # Layer 1: Application — use cases, data access contract, interfaces
    │   │   ├── db/                                 # Data access contract — owned by the application layer
    │   │   │   ├── sqlc.yaml                       # sqlc generator configuration
    │   │   │   ├── migrations/                     # goose migration SQL files (currently empty — all tables owned by .NET)
    │   │   │   ├── schema/                         # .NET-owned table snapshots — sqlc type inference ONLY, never migrated
    │   │   │   │   ├── users.sql                   # Snapshot from AuthDbContextModelSnapshot.cs
    │   │   │   │   └── user_login_histories.sql    # Snapshot from AuthDbContextModelSnapshot.cs
    │   │   │   └── sqlc/                           # Generated by sqlc — NEVER hand-edited
    │   │   │       ├── db.go                       # DBTX interface + Queries struct
    │   │   │       ├── models.go                   # Plain Go structs mirroring table columns
    │   │   │       ├── users.sql.go                # Generated user query methods
    │   │   │       └── user_login_histories.sql.go # Generated login history query methods
    │   │   │
    │   │   ├── apperror/                           # Application error types (mirrors Application exceptions in .NET)
    │   │   │   └── errors.go                       # AppError, ValidationErrors + constructors
    │   │   ├── decorator/                          # Cross-cutting decorators — wrap Handler interfaces (like IPipelineBehavior)
    │   │   │   └── validating.go                   # ValidatingDecorator[C, R] — validates before delegating to inner handler
    │   │   ├── port/                               # Outbound port interfaces
    │   │   │   ├── jwt_provider.go                 # IJwtProvider equivalent
    │   │   │   ├── email_service.go                # IEmailService
    │   │   │   └── current_user.go                 # ICurrentUser equivalent
    │   │   │
    │   │   ├── dto/                                # Data Transfer Objects (mirrors Account/DTOs/)
    │   │   │   ├── user_profile.go                 # UserProfileDto + UserAuthDto
    │   │   │   └── token_type.go                   # TokenType constants (LOGIN, NEED_ACTIVATE, RESET_PASSWORD)
    │   │   │
    │   │   ├── command/                            # Write use cases (mirrors Account/Commands/)
    │   │   │   ├── register/
    │   │   │   │   ├── command.go                  # Command struct + validation tags
    │   │   │   │   ├── handler.go                  # Handler interface + concrete implementation
    │   │   │   │   ├── validator.go                # Custom password_strength validator function
    │   │   │   │   └── register.sql                # sqlc: INSERT INTO users ...
    │   │   │   ├── login/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   └── login.sql                   # sqlc: GetUserByEmail, CreateLoginHistory
    │   │   │   ├── logout/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   └── logout.sql                  # sqlc: UpdateUserSecurityStamp, DeleteLoginHistory
    │   │   │   ├── refresh_token/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   └── refresh_token.sql           # sqlc: GetLoginHistoryByRefreshToken
    │   │   │   ├── confirm_email/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   ├── validator.go
    │   │   │   │   └── confirm_email.sql           # sqlc: GetUserByID, UpdateUserEmailConfirmed
    │   │   │   ├── change_password/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   ├── validator.go
    │   │   │   │   └── change_password.sql         # sqlc: GetUserByID, UpdateUserPasswordHash
    │   │   │   ├── change_email/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   ├── validator.go
    │   │   │   │   └── change_email.sql            # sqlc: UpdateUserEmail
    │   │   │   ├── confirm_email_change/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   ├── validator.go
    │   │   │   │   └── confirm_email_change.sql    # sqlc: GetUserByID, UpdateUserEmail
    │   │   │   ├── forgot_password/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   └── forgot_password.sql         # sqlc: GetUserByEmail
    │   │   │   ├── reset_password/
    │   │   │   │   ├── command.go
    │   │   │   │   ├── handler.go
    │   │   │   │   ├── validator.go
    │   │   │   │   └── reset_password.sql          # sqlc: GetUserByEmail, UpdateUserPasswordHash
    │   │   │   └── resend_email_confirmation/
    │   │   │       ├── command.go
    │   │   │       ├── handler.go
    │   │   │       └── resend_email_confirmation.sql # sqlc: GetUserByID
    │   │   │
    │   │   ├── query/                              # Read use cases (mirrors Account/Queries/)
    │   │   │   ├── get_profile/
    │   │   │   │   ├── query.go
    │   │   │   │   ├── handler.go
    │   │   │   │   └── get_profile.sql             # sqlc: GetUserByID
    │   │   │   └── get_user_login_histories/
    │   │   │       ├── query.go
    │   │   │       ├── handler.go
    │   │   │       └── get_user_login_histories.sql # sqlc: ListLoginHistoriesByUser
    │   │   │
    │   │   └── event/                              # Domain events (mirrors RegisterEvent, ForgotPasswordEvent)
    │   │       ├── event.go                        # Event interface + all concrete event types
    │   │       └── dispatcher.go                   # Dispatcher interface (replaces MediatR IPublisher)
    │   │
    │   ├── infrastructure/                         # Layer 3: Infrastructure — external dependencies
    │   │   ├── persistence/                        # Database connection
    │   │   │   └── db.go                           # sql.Open + goose migrations + returns *db.Queries
    │   │   │
    │   │   ├── identity/                           # Auth (mirrors Auth.Infrastructure/Identity/)
    │   │   │   ├── jwt_provider.go                 # Implements port.JwtProvider (golang-jwt/jwt/v5)
    │   │   │   └── jwt_config.go                   # JwtConfig struct loaded from env
    │   │   │
    │   │   ├── notification/                       # Email (mirrors Auth.Infrastructure/Notifications/)
    │   │   │   ├── email_service.go                # Implements port.EmailService (Mailjet/SMTP)
    │   │   │   └── mailer/
    │   │   │       ├── send_register_confirm_email.go  # Handles RegisterEvent + ResendEmailConfirmationEvent
    │   │   │       ├── send_reset_password.go          # Handles ForgotPasswordEvent
    │   │   │       └── send_confirm_email_change.go    # Handles ChangeEmailEvent
    │   │   │
    │   │   └── config/
    │   │       └── config.go                       # Loads credentials/appsettings*.json with env overlay
    │   │
    │   └── transport/
    │       └── http/                               # Layer 4: Presentation (mirrors WebAPI/Controllers/)
    │           ├── server.go                       # net/http server setup + middleware chain + route registration
    │           ├── middleware/
    │           │   ├── auth_middleware.go           # JWT cookie extraction + validation (JwtBearerSetup)
    │           │   └── performance_middleware.go    # Slow request logging (PerformanceBehavior)
    │           └── handler/
    │               └── auth_handler.go             # All /api/auth/* routes (AuthController equivalent)
    │
    │
    ├── go.mod
    ├── go.sum
    └── README.md
```

---

## 4. Layer-by-Layer Design

### 4.1 Application Layer (`internal/application/`)

#### Outbound Port Interfaces (`internal/application/port/`)

These define _what_ the application needs from infrastructure without knowing _how_ it is implemented. Kept minimal — only concerns that are genuinely cross-cutting (JWT, email, current user). DB access goes directly via `*sqlc.Queries`, which lives in `internal/application/db/sqlc/` — part of the application layer, not infrastructure.

> **No `IIdentityService`, no `IAuthRepository`:** Each command handler receives `*sqlc.Queries` directly and owns all its DB logic. The SQL query files that generate `*sqlc.Queries` already live in the application layer (`application/command/*/`, `application/query/*/`), so the generated types logically belong there too. Adding a repository interface would split a single use case across two files with no benefit.

```go
// internal/application/port/jwt_provider.go
package port

import (
    "time"
    "auth/internal/application/dto"
)

type TokenPair struct {
    AccessToken         string
    RefreshToken        string
    AccessTokenExpires  time.Time
    RefreshTokenExpires time.Time
}

type JwtProvider interface {
    GenerateTokens(profile dto.UserProfile) (TokenPair, error)
    ValidateRefreshToken(refreshToken string) (*dto.UserProfile, error)
}
```

#### DTOs (`internal/application/dto/`)

```go
// internal/application/dto/user_profile.go
package dto

type UserProfile struct {
    Id             string
    Email          string
    FirstName      string
    LastName       string
    EmailConfirmed bool
}

// UserAuth mirrors UserAuthDto — tokens go into cookies, not JSON response body.
// Expiry fields live here (session concern), not on UserProfile (identity concern).
type UserAuth struct {
    UserProfile
    AccessToken         string `json:"-"`
    RefreshToken        string `json:"-"`
    AccessTokenExpires  int64  // Unix milliseconds
    RefreshTokenExpires int64  // Unix milliseconds
}
```

#### Events (`internal/application/event/`)

```go
// internal/application/event/event.go
package event

import (
    "context"
    "auth/internal/application/dto"
)

type Event interface{ eventMarker() }

type RegisterEvent struct {
    User dto.UserProfile
    Code string
}
func (RegisterEvent) eventMarker() {}

type ForgotPasswordEvent struct {
    Email string
    Code  string
}
func (ForgotPasswordEvent) eventMarker() {}

type ChangeEmailEvent struct {
    User     dto.UserProfile
    Code     string
    NewEmail string
}
func (ChangeEmailEvent) eventMarker() {}

type ResendEmailConfirmationEvent struct {
    User dto.UserProfile
    Code string
}
func (ResendEmailConfirmationEvent) eventMarker() {}

// Dispatcher replaces MediatR's IPublisher.
type Dispatcher interface {
    Dispatch(ctx context.Context, event Event) error
}
```

### 4.2 Infrastructure Layer (`internal/infrastructure/`)

The infrastructure layer's database responsibility is narrow: open the SQL Server connection, run pending goose migrations, and return `*db.Queries` to `main.go`. Everything else — the query definitions, schema snapshots, sqlc configuration, and generated Go types — lives in the application layer (`internal/application/db/`), because the application owns its data access contract.

#### SQL Schema and Query Files (`internal/application/db/`)

SQL query files live alongside the command or query package that uses them. sqlc reads them to generate type-safe Go code. goose runs migrations at startup.

> **All tables are currently owned by the .NET EF Core migrations** (`Users` and `UserLoginHistories` both appear in `AuthDbContextModelSnapshot.cs`). The Go app has no migrations of its own — `db/migrations/` is empty. If Go needs an exclusive table in the future, add a goose migration file there.

> **Schema snapshots** in `db/schema/` are derived from `AuthDbContextModelSnapshot.cs` and kept in sync manually whenever the .NET schema changes. They are never applied to the database — they exist solely for sqlc type inference. Column types match the .NET model exactly (e.g. `AccessToken NVARCHAR(500)`, `IpAddress NVARCHAR(39)`, `CreatedAt DATETIME2`).


**Note:** `-- +goose StatementBegin/End` wrappers are required for MSSQL — goose needs explicit statement boundaries since T-SQL batches are not delimited by `;` in the same way as other dialects.

SQL query files live alongside the command or query package that uses them. Each file contains only the SQL statements needed by that specific use case. sqlc scans all `command/` and `query/` subdirectories recursively.

```sql
-- internal/application/command/login/login.sql

-- name: GetUserByEmail :one
SELECT * FROM Users WHERE NormalizedEmail = @normalized_email;

-- name: CreateLoginHistory :one
INSERT INTO UserLoginHistories (UserId, IpAddress, UserAgent, AccessToken, RefreshToken, CreatedAt)
OUTPUT INSERTED.*
VALUES (@user_id, @ip_address, @user_agent, @access_token, @refresh_token, @created_at);
```

```sql
-- internal/application/command/register/register.sql

-- name: CreateUser :exec
-- Inserts a new user into the shared .NET Identity Users table.
-- EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
-- AccessFailedCount default to 0; PhoneNumber and LockoutEnd default to NULL.
INSERT INTO Users (
    Id, UserName, NormalizedUserName,
    Email, NormalizedEmail,
    EmailConfirmed,
    PasswordHash, SecurityStamp, ConcurrencyStamp,
    PhoneNumber, PhoneNumberConfirmed,
    TwoFactorEnabled,
    LockoutEnd, LockoutEnabled, AccessFailedCount,
    FirstName, LastName
) VALUES (
    @id, @user_name, @normalized_user_name,
    @email, @normalized_email,
    0,
    @password_hash, @security_stamp, @concurrency_stamp,
    NULL, 0,
    0,
    NULL, 0, 0,
    @first_name, @last_name
);
```

```sql
-- internal/application/command/confirm_email/confirm_email.sql

-- name: GetUserSecurityStamp :one
-- Used to validate the HMAC token: check SecurityStamp in token matches DB.
SELECT SecurityStamp FROM Users WHERE Id = @id;

-- name: ConfirmUserEmail :exec
UPDATE Users SET EmailConfirmed = 1 WHERE Id = @id;
```

```sql
-- internal/application/command/logout/logout.sql

-- name: UpdateUserSecurityStamp :exec
UPDATE Users SET SecurityStamp = @security_stamp WHERE Id = @id;

-- name: DeleteLoginHistory :exec
DELETE FROM UserLoginHistories WHERE Id = @id;
```

```sql
-- internal/application/query/get_user_login_histories/get_user_login_histories.sql

-- name: ListLoginHistoriesByUser :many
SELECT * FROM UserLoginHistories
WHERE UserId = @user_id
ORDER BY Id DESC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;
```

**T-SQL conventions:** `go-mssqldb` requires named parameters (`@name`). `OUTPUT INSERTED.*` replaces `RETURNING *`. `OFFSET/FETCH` replaces `LIMIT/OFFSET`.

**Shared queries:** A sqlc query name must be unique across all `.sql` files. When multiple commands need the same operation (e.g. `GetUserByID` is used by `confirm_email`, `change_password`, and `get_profile`), define it in one file — the generated Go method is shared across all callers.

#### sqlc Configuration

```yaml
# internal/application/db/sqlc.yaml
version: "2"
sql:
  - engine: "sqlserver"
    queries:
      - "../command/"
      - "../query/"
    schema:
      - "migrations/"   # Go-owned tables (currently none)
      - "schema/"       # .NET-owned table snapshots — for sqlc type inference only
    gen:
      go:
        package: "sqlc"
        out: "sqlc"   # generates into internal/application/db/sqlc/
        emit_interface: false
        emit_json_tags: false
        emit_db_tags: false
        emit_pointers_for_null_types: true
```

Run `sqlc generate -f internal/application/db/sqlc.yaml` to regenerate `internal/application/db/sqlc/`. Those files are committed but never hand-edited.

sqlc recursively scans each listed directory, picking up every `.sql` file inside `command/login/`, `command/register/`, `query/get_profile/`, etc. Each sqlc-named query (e.g. `-- name: GetUserByEmail :one`) must be unique across all `.sql` files. If two commands need the same database operation (e.g. both `forgot_password` and `login` need `GetUserByEmail`), define it in one file only — the generated method in `persistence/sqlc/` is shared across all callers.

#### DB Connection (`persistence/db.go`)

Replaces `gorm.Open` + `AutoMigrate` with `database/sql` + embedded goose migrations:

```go
//go:generate sqlc generate -f ../../application/db/sqlc.yaml
package persistence

import (
    "context"
    "database/sql"
    "embed"

    "github.com/pressly/goose/v3"
    _ "github.com/microsoft/go-mssqldb"

    db "auth/internal/application/db/sqlc"
)

//go:embed ../../application/db/migrations/*.sql
var migrations embed.FS

// NewDB opens an MSSQL connection, runs pending goose migrations, and returns *db.Queries.
// dsn: "sqlserver://user:password@host:1433?database=auth"
func NewDB(ctx context.Context, dsn string) (*db.Queries, error) {
    conn, err := sql.Open("sqlserver", dsn)
    if err != nil {
        return nil, err
    }
    if err := conn.PingContext(ctx); err != nil {
        return nil, err
    }

    goose.SetBaseFS(migrations)
    if err := goose.SetDialect("mssql"); err != nil {
        return nil, err
    }
    if err := goose.RunContext(ctx, "up", conn, "migrations"); err != nil {
        return nil, err
    }

    return db.New(conn), nil
}
```

> **Production note:** `goose.RunContext` runs on every startup. In a single-instance deployment this is fine. In a multi-instance deployment (e.g. Kubernetes rolling update), multiple replicas can start simultaneously and race on the same migration. goose uses a lock table to reduce the risk, but the safer pattern is to run migrations as a separate pre-deploy step and disable auto-migration in production via an environment flag:
>
> ```bash
> # Run once before deploying new instances (CI pipeline, Kubernetes Job, etc.)
> goose -dir internal/application/db/migrations mssql "$DATABASE_DSN" up
> ```
>
> ```go
> // In NewDB: skip goose when RUN_MIGRATIONS=false
> if os.Getenv("RUN_MIGRATIONS") != "false" {
>     goose.SetBaseFS(migrations)
>     goose.SetDialect("mssql")
>     goose.RunContext(ctx, "up", conn, "migrations")
> }
> ```
>
> Auto-migration remains on by default, which is correct for local development and CI.

#### JWT Provider

```go
// internal/infrastructure/identity/jwt_provider.go
package identity

import (
    "fmt"
    "time"
    "auth/internal/application/dto"
    "auth/internal/application/port"
    "github.com/golang-jwt/jwt/v5"
)

const (
    accessTokenExpiryMinutes  = 5
    refreshTokenExpiryMinutes = 5 * 60
)

type authClaims struct {
    jwt.RegisteredClaims
    Id                  string `json:"id"`
    EmailCustom         string `json:"emailCus"`
    FirstName           string `json:"firstName"`
    LastName            string `json:"lastName"`
    EmailConfirmed      bool   `json:"emailConfirmed"`
    RefreshTokenExpires int64  `json:"rte,omitempty"`
    Type                string `json:"type"`
}

type jwtProvider struct{ cfg JwtConfig }

func NewJwtProvider(cfg JwtConfig) port.JwtProvider { return &jwtProvider{cfg: cfg} }

func (p *jwtProvider) GenerateTokens(profile dto.UserProfile) (port.TokenPair, error) {
    accessExpires  := time.Now().UTC().Add(accessTokenExpiryMinutes * time.Minute)
    refreshExpires := time.Now().UTC().Add(refreshTokenExpiryMinutes * time.Minute)

    tokenType := "LOGIN"
    if !profile.EmailConfirmed {
        tokenType = "NEED_ACTIVATE"
    }

    accessToken, err := p.sign(profile, p.cfg.AccessTokenSecretKey, accessExpires, &refreshExpires, tokenType)
    if err != nil {
        return port.TokenPair{}, err
    }
    refreshToken, err := p.sign(profile, p.cfg.RefreshTokenSecretKey, refreshExpires, nil, tokenType)
    if err != nil {
        return port.TokenPair{}, err
    }

    return port.TokenPair{
        AccessToken: accessToken, RefreshToken: refreshToken,
        AccessTokenExpires: accessExpires, RefreshTokenExpires: refreshExpires,
    }, nil
}

func (p *jwtProvider) ValidateRefreshToken(refreshToken string) (*dto.UserProfile, error) {
    token, err := jwt.ParseWithClaims(refreshToken, &authClaims{}, func(t *jwt.Token) (interface{}, error) {
        if _, ok := t.Method.(*jwt.SigningMethodHMAC); !ok {
            return nil, fmt.Errorf("unexpected signing method: %v", t.Header["alg"])
        }
        return []byte(p.cfg.RefreshTokenSecretKey), nil
    })
    if err != nil || !token.Valid {
        return nil, nil
    }
    claims := token.Claims.(*authClaims)
    return &dto.UserProfile{
        Id: claims.Id, Email: claims.EmailCustom,
        FirstName: claims.FirstName, LastName: claims.LastName,
        EmailConfirmed: claims.EmailConfirmed,
    }, nil
}
```

#### Config (`internal/infrastructure/config/config.go`)

Config is loaded from the shared `credentials/` folder at the repository root — the same files used by the .NET app. No credentials are copied into `golang/`. The binary is run from `golang/auth/`, so the path is `../../credentials/`.

Loading order (mirrors .NET's `appsettings` layering):
1. `../../credentials/appsettings.json` — base values
2. `../../credentials/appsettings.<APP_ENV>.json` — environment overlay (defaults to `Production`)

Each root JSON object that the auth module uses maps to its own struct. Unknown keys (e.g. `Serilog`, `BnbSetting`) are silently ignored by the JSON decoder.

```go
// internal/infrastructure/config/config.go
package config

import (
    "encoding/json"
    "fmt"
    "os"
)

// Config holds only the root JSON objects relevant to the auth module.
// Unknown keys in the JSON files are ignored.
type Config struct {
    ConnectionStrings ConnectionStringsConfig `json:"ConnectionStrings"`
    Application       ApplicationConfig       `json:"Application"`
    Email             EmailConfig             `json:"Email"`
    Jwt               JwtConfig               `json:"Jwt"`
}

type ConnectionStringsConfig struct {
    // ADO.NET connection string — passed directly to sql.Open("sqlserver", ...).
    // go-mssqldb accepts both URL and ADO.NET formats.
    AuthConnection string `json:"AuthConnection"`
}

type ApplicationConfig struct {
    Version  string `json:"Version"`
    Endpoint string `json:"Endpoint"` // base URL for email confirmation links
}

type EmailConfig struct {
    ApiKeyPublic  string `json:"ApiKeyPublic"`
    ApiKeyPrivate string `json:"ApiKeyPrivate"`
    FromEmail     string `json:"FromEmail"`
}

type JwtConfig struct {
    Issuer                string `json:"Issuer"`
    Audience              string `json:"Audience"`
    AccessTokenSecretKey  string `json:"AccessTokenSecretKey"`
    RefreshTokenSecretKey string `json:"RefreshTokenSecretKey"`
}

const credentialsDir = "../../credentials"

// Load reads appsettings.json then overlays the environment-specific file.
// Set APP_ENV=Development or APP_ENV=Production (default: Production).
func Load() Config {
    env := os.Getenv("APP_ENV")
    if env == "" {
        env = "Production"
    }

    base    := readFile(fmt.Sprintf("%s/appsettings.json", credentialsDir))
    overlay := readFile(fmt.Sprintf("%s/appsettings.%s.json", credentialsDir, env))

    merged, err := mergeJSON(base, overlay)
    if err != nil {
        panic(fmt.Sprintf("config merge failed: %v", err))
    }

    var cfg Config
    if err := json.Unmarshal(merged, &cfg); err != nil {
        panic(fmt.Sprintf("config parse failed: %v", err))
    }
    return cfg
}

// readFile returns file bytes, or an empty JSON object if the file does not exist.
func readFile(path string) []byte {
    data, err := os.ReadFile(path)
    if err != nil {
        return []byte("{}")
    }
    return data
}

// mergeJSON performs a shallow merge: overlay root keys overwrite base root keys.
// This mirrors .NET's appsettings layering behaviour at the root level.
func mergeJSON(base, overlay []byte) ([]byte, error) {
    var b, o map[string]json.RawMessage
    if err := json.Unmarshal(base, &b); err != nil {
        return nil, err
    }
    if err := json.Unmarshal(overlay, &o); err != nil {
        return nil, err
    }
    for k, v := range o {
        b[k] = v
    }
    return json.Marshal(b)
}
```

### 4.3 Presentation Layer (`internal/transport/http/`)

Go 1.22+ `ServeMux` supports method-prefixed patterns and path parameters natively — no third-party router needed.

**Custom handler type** for clean error propagation (replaces Gin's `c.Error()`):

```go
// internal/transport/http/handler/handler.go
package handler

// AppHandlerFunc is a http.HandlerFunc that can return an error.
// The error middleware wraps it and maps errors to HTTP responses.
type AppHandlerFunc func(w http.ResponseWriter, r *http.Request) error

func (f AppHandlerFunc) ServeHTTP(w http.ResponseWriter, r *http.Request) {
    if err := f(w, r); err != nil {
        writeError(w, err)
    }
}

func writeError(w http.ResponseWriter, err error) {
    w.Header().Set("Content-Type", "application/json")
    switch e := err.(type) {
    case *apperror.AppError:
        w.WriteHeader(e.Code)
        _ = json.NewEncoder(w).Encode(map[string]string{"message": e.Message})
    case *apperror.ValidationErrors:
        w.WriteHeader(http.StatusUnprocessableEntity)
        _ = json.NewEncoder(w).Encode(e)
    default:
        w.WriteHeader(http.StatusInternalServerError)
        _ = json.NewEncoder(w).Encode(map[string]string{"message": "Internal Server Error"})
    }
}
```

**Route registration** using Go 1.22 `ServeMux` method+path patterns:

```go
// internal/transport/http/handler/auth_handler.go
type AuthHandler struct {
    registerHandler           register.Handler
    loginHandler              login.Handler
    refreshTokenHandler       refresh_token.Handler
    logoutHandler             logout.Handler
    confirmEmailHandler       confirm_email.Handler
    changePasswordHandler     change_password.Handler
    changeEmailHandler        change_email.Handler
    confirmEmailChangeHandler confirm_email_change.Handler
    forgotPasswordHandler     forgot_password.Handler
    resetPasswordHandler      reset_password.Handler
    resendEmailHandler        resend_email_confirmation.Handler
    getProfileHandler         get_profile.Handler
    getLoginHistoriesHandler  get_user_login_histories.Handler
    isDevelopment             bool
}

func (h *AuthHandler) RegisterRoutes(mux *http.ServeMux, authMW func(http.Handler) http.Handler) {
    // Public routes
    mux.Handle("POST /api/auth/register",         AppHandlerFunc(h.Register))
    mux.Handle("POST /api/auth/login",            AppHandlerFunc(h.Login))
    mux.Handle("POST /api/auth/refresh",          AppHandlerFunc(h.Refresh))
    mux.Handle("GET  /api/auth/confirm-email",    AppHandlerFunc(h.ConfirmEmail))
    mux.Handle("POST /api/auth/forgot-password",  AppHandlerFunc(h.ForgotPassword))
    mux.Handle("POST /api/auth/reset-password",   AppHandlerFunc(h.ResetPassword))

    // Protected routes — wrapped with auth middleware
    mux.Handle("GET  /api/auth/check",                       authMW(AppHandlerFunc(h.Check)))
    mux.Handle("POST /api/auth/logout",                      authMW(AppHandlerFunc(h.Logout)))
    mux.Handle("GET  /api/auth/login-histories",             authMW(AppHandlerFunc(h.GetLoginHistories)))
    mux.Handle("POST /api/auth/change-password",             authMW(AppHandlerFunc(h.ChangePassword)))
    mux.Handle("POST /api/auth/change-email",                authMW(AppHandlerFunc(h.ChangeEmail)))
    mux.Handle("POST /api/auth/confirm-email-change",        authMW(AppHandlerFunc(h.ConfirmEmailChange)))
    mux.Handle("POST /api/auth/resend-email-confirmation",   authMW(AppHandlerFunc(h.ResendEmailConfirmation)))
}

func (h *AuthHandler) Login(w http.ResponseWriter, r *http.Request) error {
    var cmd login.Command
    if err := json.NewDecoder(r.Body).Decode(&cmd); err != nil {
        return apperror.NewBadRequest("invalid request body")
    }
    cmd.IPAddress = clientIP(r)
    cmd.UserAgent = r.Header.Get("User-Agent")

    result, err := h.loginHandler.Handle(r.Context(), cmd)
    if err != nil {
        return err
    }

    h.setTokenCookies(w, result.AccessToken, result.RefreshToken,
        result.AccessTokenExpires, result.RefreshTokenExpires)
    w.Header().Set("Content-Type", "application/json")
    return json.NewEncoder(w).Encode(result.UserProfile)
}

// clientIP reads the real client IP, checking X-Forwarded-For first.
func clientIP(r *http.Request) string {
    if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
        return strings.SplitN(xff, ",", 2)[0]
    }
    ip, _, _ := net.SplitHostPort(r.RemoteAddr)
    return ip
}
```

---

## 5. CQRS Adaptation in Go

In .NET, MediatR provides a runtime bus: `ISender.Send(command)` resolves the handler via reflection. In Go this is unnecessary — each command/query package defines its own small `Handler` interface and the HTTP layer calls it directly.

```
HTTP Handler → h.loginHandler.Handle(ctx, cmd)    → (result, error)
HTTP Handler → h.getProfileHandler.Handle(ctx, q) → (result, error)
```

**Per-package Handler Interface Pattern:**

```go
// internal/application/command/register/handler.go
package register

import "context"

type Handler interface {
    Handle(ctx context.Context, cmd Command) (Result, error)
}

type handler struct {
    db          *db.Queries
    dispatcher  event.Dispatcher
    logger      *slog.Logger
    tokenSecret string
}

// NewHandler wires the register handler.
// tokenSecret is used to HMAC-sign email confirmation tokens.
// Validation is not performed here — wrap with decorator.NewValidating at wiring time.
func NewHandler(queries *db.Queries, dispatcher event.Dispatcher, logger *slog.Logger, tokenSecret string) Handler {
    return &handler{db: queries, dispatcher: dispatcher, logger: logger, tokenSecret: tokenSecret}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
    user, code, err := h.createUser(ctx, cmd)
    if err != nil {
        return Result{}, err
    }

    // Fire-and-forget: context.WithoutCancel so email delivery is not aborted
    // when the HTTP request context is cancelled (e.g. client disconnects).
    go func() {
        dispatchCtx := context.WithoutCancel(ctx)
        if dispatchErr := h.dispatcher.Dispatch(dispatchCtx, event.RegisterEvent{User: user, Code: code}); dispatchErr != nil {
            h.logger.ErrorContext(ctx, "failed to dispatch RegisterEvent", slog.Any("error", dispatchErr))
        }
    }()

    return Result{UserId: user.Id}, nil
}

// createUser implements the five steps described in REGISTER.md.
// This is the Go equivalent of IdentityService.CreateUserAsync in the .NET app.
func (h *handler) createUser(ctx context.Context, cmd Command) (dto.UserProfile, string, error) {
    // Step 1 — Hash password (PBKDF2-HMAC-SHA256, ASP.NET Identity V3 format)
    hash, err := hashPasswordIdentityV3(cmd.Password)
    if err != nil {
        return dto.UserProfile{}, "", err
    }

    // Step 2 — Normalize email and generate identifiers
    email            := strings.ToLower(cmd.Email)
    normalizedEmail  := strings.ToUpper(cmd.Email)
    userId           := uuid.New().String()
    securityStamp    := uuid.New().String()
    concurrencyStamp := uuid.New().String()

    // Step 3 — Insert into the shared .NET Identity Users table (17 columns)
    err = h.db.CreateUser(ctx, db.CreateUserParams{
        Id:                 userId,
        UserName:           email,
        NormalizedUserName: normalizedEmail,
        Email:              email,
        NormalizedEmail:    normalizedEmail,
        PasswordHash:       hash,
        SecurityStamp:      securityStamp,
        ConcurrencyStamp:   concurrencyStamp,
        FirstName:          cmd.FirstName,
        LastName:           cmd.LastName,
    })
    if err != nil {
        var mssqlErr *mssql.Error
        if errors.As(err, &mssqlErr) && (mssqlErr.Number == 2627 || mssqlErr.Number == 2601) {
            return dto.UserProfile{}, "", apperror.NewConflict("email is already taken")
        }
        return dto.UserProfile{}, "", err
    }

    // Step 4 — Generate stateless HMAC-SHA256 email confirmation token
    // Mirrors .NET DataProtectorTokenProvider: no DB table, encodes SecurityStamp + expiry
    expiresAt := time.Now().UTC().Add(24 * time.Hour)
    payload   := fmt.Sprintf("%s|%s|%d", userId, securityStamp, expiresAt.Unix())
    mac       := hmac.New(sha256.New, []byte(h.tokenSecret))
    mac.Write([]byte("EmailConfirmation|" + payload))
    sig  := mac.Sum(nil)
    code := base64.RawURLEncoding.EncodeToString([]byte(payload + "|" + hex.EncodeToString(sig)))

    // Step 5 — Return
    return dto.UserProfile{
        Id: userId, Email: email,
        FirstName: cmd.FirstName, LastName: cmd.LastName,
        EmailConfirmed: false,
    }, code, nil
}
```

**Command struct with validation tags** (replacing FluentValidation):

```go
// internal/application/command/register/command.go
package register

type Command struct {
    Email     string `json:"email"     validate:"required,email,max=255"`
    Password  string `json:"password"  validate:"required,min=6,password_strength"`
    FirstName string `json:"firstName" validate:"required,min=2,max=50"`
    LastName  string `json:"lastName"  validate:"required,min=2,max=50"`
}

type Result struct {
    UserId string `json:"userId"`
}
```

**Custom validator** (replacing FluentValidation `.Matches()`):

```go
// internal/application/command/register/validator.go
package register

import (
    "regexp"
    "github.com/go-playground/validator/v10"
)

var (
    hasLower   = regexp.MustCompile(`[a-z]`)
    hasUpper   = regexp.MustCompile(`[A-Z]`)
    hasDigit   = regexp.MustCompile(`[0-9]`)
    hasSpecial = regexp.MustCompile(`[!@#$%^&*()\-_+=\[{\]};:<>|./?@,]`)
)

func PasswordStrength(fl validator.FieldLevel) bool {
    p := fl.Field().String()
    return hasLower.MatchString(p) && hasUpper.MatchString(p) &&
           hasDigit.MatchString(p) && hasSpecial.MatchString(p)
}
```

**Decorator Pattern** (replaces `IPipelineBehavior<,>` / `ValidationBehavior`):

Validation is a cross-cutting concern — it should not live inside business logic. Instead of calling `apperror.Validate` at the top of every `Handle()`, a `ValidatingDecorator` wraps any handler and runs validation before delegating. This mirrors exactly how `ValidationBehavior : IPipelineBehavior<TReq, TRes>` works in .NET — except wiring is explicit in `cmd/main.go` rather than registered via DI.

```go
// internal/application/decorator/validating.go
package decorator

import (
    "context"

    "github.com/go-playground/validator/v10"
    "auth/internal/application/apperror"
)

// ValidatingDecorator[C, R] wraps any handler and runs struct validation first.
// Because Go uses structural typing, *ValidatingDecorator[register.Command, register.Result]
// automatically satisfies register.Handler — no explicit interface assertion needed.
type ValidatingDecorator[C any, R any] struct {
    inner    interface{ Handle(context.Context, C) (R, error) }
    validate *validator.Validate
}

func NewValidating[C any, R any](
    inner interface{ Handle(context.Context, C) (R, error) },
    v *validator.Validate,
) *ValidatingDecorator[C, R] {
    return &ValidatingDecorator[C, R]{inner: inner, validate: v}
}

func (d *ValidatingDecorator[C, R]) Handle(ctx context.Context, cmd C) (R, error) {
    if err := apperror.Validate(d.validate, cmd); err != nil {
        var zero R
        return zero, err
    }
    return d.inner.Handle(ctx, cmd)
}
```

Custom validators (e.g. `password_strength`) are registered once on a shared `*validator.Validate` instance in `cmd/main.go` and injected into the decorator — not inside the handler itself:

```go
// cmd/main.go — validator setup
func newValidator() *validator.Validate {
    v := validator.New()
    v.RegisterTagNameFunc(func(fld reflect.StructField) string {
        name := strings.SplitN(fld.Tag.Get("json"), ",", 2)[0]
        if name == "-" {
            return ""
        }
        return name
    })
    _ = v.RegisterValidation("password_strength", register.PasswordStrength)
    return v
}
```

---

## 6. Dependency Injection

Manual wiring in `cmd/main.go` — explicit, readable, zero magic. This replaces `DependencyInjection.cs` in both the Application and Infrastructure layers.

```go
// cmd/main.go
func main() {
    logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
    cfg    := config.Load()

    // Infrastructure — database: sql.Open + goose migrations + sqlc.New
    // cfg.ConnectionStrings.AuthConnection is the ADO.NET connection string from appsettings.json
    queries, err := persistence.NewDB(context.Background(), cfg.ConnectionStrings.AuthConnection)
    if err != nil {
        logger.Error("failed to open database", slog.Any("error", err))
        os.Exit(1)
    }

    // Infrastructure — all handlers receive *sqlc.Queries directly, no repository layer
    jwtProvider  := identity.NewJwtProvider(cfg.Jwt)
    emailService := notification.NewEmailService(cfg.Email)

    // Event dispatcher — replaces MediatR TaskWhenAllPublisher
    // All handlers for the same event run concurrently via sync.WaitGroup
    dispatcher := buildDispatcher(emailService, cfg.Application)

    // Shared validator — custom tags registered once, reused by all decorators
    v := newValidator()

    // Application: Command & Query Handlers
    // Each raw handler contains only business logic — no validation.
    // Wrap with decorator.NewValidating to add validation as a decorator layer,
    // mirroring ValidationBehavior : IPipelineBehavior<,> in .NET.
    registerHandler := decorator.NewValidating[register.Command, register.Result](
        register.NewHandler(queries, dispatcher, logger, cfg.Jwt.AccessTokenSecretKey),
        v,
    )
    loginHandler := decorator.NewValidating[login.Command, login.Result](
        login.NewHandler(queries, jwtProvider, logger),
        v,
    )
    // ... all other handlers wrapped the same way

    // Presentation
    authHandler := handler.NewAuthHandler(registerHandler, loginHandler /* ... */)
    http.NewServer(cfg.Port, authHandler, logger).Run()
}

func buildDispatcher(emailSvc port.EmailService, appCfg config.ApplicationConfig) event.Dispatcher {
    sendRegister    := mailer.NewSendRegisterConfirmEmail(emailSvc, appCfg)
    sendForgotPw    := mailer.NewSendResetPassword(emailSvc, appCfg)
    sendChangeEmail := mailer.NewSendConfirmEmailChange(emailSvc, appCfg)

    return &simpleDispatcher{
        handlers: map[string][]eventHandlerFunc{
            "RegisterEvent":                {sendRegister.HandleRegisterEvent},
            "ForgotPasswordEvent":          {sendForgotPw.Handle},
            "ChangeEmailEvent":             {sendChangeEmail.Handle},
            "ResendEmailConfirmationEvent": {sendRegister.HandleResendEvent},
        },
    }
}
```

---

## 7. Error Handling

Typed errors with HTTP status codes replace the .NET exception hierarchy (`BadRequestException`, `NotFoundException`, etc.):

```go
// internal/application/apperror/errors.go
package apperror

type AppError struct {
    Code    int
    Message string
}
func (e *AppError) Error() string { return e.Message }

func NewBadRequest(msg string) *AppError    { return &AppError{Code: 400, Message: msg} }
func NewNotFound(msg string) *AppError      { return &AppError{Code: 404, Message: msg} }
func NewConflict(msg string) *AppError      { return &AppError{Code: 409, Message: msg} }
func NewUnprocessable(msg string) *AppError { return &AppError{Code: 422, Message: msg} }

// ValidationErrors mirrors UnprocessableEntityException
type ValidationErrors struct {
    Fields []FieldError `json:"errors"`
}
type FieldError struct {
    Name   string   `json:"name"`
    Errors []string `json:"errors"`
}
func (e *ValidationErrors) Error() string { return "validation failed" }

// Validate runs struct validation and maps errors to *ValidationErrors.
// Call this at the top of every Handle() method before any business logic.
func Validate(v *validator.Validate, cmd any) error {
    if err := v.Struct(cmd); err != nil {
        var ve validator.ValidationErrors
        if !errors.As(err, &ve) {
            return err
        }
        grouped := make(map[string][]string)
        for _, fe := range ve {
            grouped[fe.Field()] = append(grouped[fe.Field()], fe.Tag())
        }
        result := &ValidationErrors{}
        for name, errs := range grouped {
            result.Fields = append(result.Fields, FieldError{Name: name, Errors: errs})
        }
        return result
    }
    return nil
}
```

Errors propagate via the `AppHandlerFunc` return value and are written by `writeError` (defined in section 4.4) — equivalent to `ExceptionMiddleware.cs`. No separate middleware needed; error mapping is centralized in one function.

---

## 8. Middleware Pipeline

`net/http` middleware uses the standard `func(http.Handler) http.Handler` wrapping pattern. A `Chain` helper applies them in order, mirroring the MediatR pipeline behavior registration sequence.

```go
// internal/transport/http/server.go
func NewServer(port string, authHandler *handler.AuthHandler, jwtProvider port.JwtProvider, logger *slog.Logger) *http.Server {
    mux := http.NewServeMux()

    authMW := middleware.Auth(jwtProvider)
    authHandler.RegisterRoutes(mux, authMW)

    // Wrap the entire mux with global middleware (innermost → outermost)
    var h http.Handler = mux
    h = middleware.Performance(logger)(h) // PerformanceBehavior — warns >500ms
    h = middleware.Recovery(logger)(h)    // UnhandledExceptionBehavior — panic recovery

    return &http.Server{Addr: ":" + port, Handler: h}
}
```

**Middleware signature** — standard Go pattern, composable and testable:

```go
// internal/transport/http/middleware/performance_middleware.go
func Performance(logger *slog.Logger) func(http.Handler) http.Handler {
    return func(next http.Handler) http.Handler {
        return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
            start := time.Now()
            next.ServeHTTP(w, r)
            elapsed := time.Since(start)

            level := slog.LevelInfo
            if elapsed > 500*time.Millisecond {
                level = slog.LevelWarn
            }
            logger.Log(r.Context(), level, "request processed",
                slog.String("path", r.URL.Path),
                slog.Duration("elapsed", elapsed),
            )
        })
    }
}
```

**JWT Auth Middleware** mirrors `JwtBearerSetup` — reads `accessToken` cookie and stores the current user in `context.Context` (replaces Gin's `c.Set`):

```go
// internal/transport/http/middleware/auth_middleware.go
type contextKey string
const CurrentUserKey contextKey = "currentUser"

func Auth(jwtProvider port.JwtProvider) func(http.Handler) http.Handler {
    return func(next http.Handler) http.Handler {
        return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
            cookie, err := r.Cookie("accessToken")
            if err != nil || cookie.Value == "" {
                w.Header().Set("Content-Type", "application/json")
                w.WriteHeader(http.StatusUnauthorized)
                _ = json.NewEncoder(w).Encode(map[string]string{"message": "Unauthorized"})
                return
            }
            profile, err := jwtProvider.ValidateAccessToken(cookie.Value)
            if err != nil || profile == nil {
                w.Header().Set("Content-Type", "application/json")
                w.WriteHeader(http.StatusUnauthorized)
                _ = json.NewEncoder(w).Encode(map[string]string{"message": "Unauthorized"})
                return
            }
            ctx := context.WithValue(r.Context(), CurrentUserKey, profile)
            next.ServeHTTP(w, r.WithContext(ctx))
        })
    }
}

// CurrentUserFrom extracts the current user from context — used inside handlers.
func CurrentUserFrom(ctx context.Context) (*dto.UserProfile, bool) {
    p, ok := ctx.Value(CurrentUserKey).(*dto.UserProfile)
    return p, ok
}
```

**Cookie strategy** mirrors the .NET controller — `HttpOnly`, `Secure` in production, `SameSite: Lax` in dev / `None` in prod:

```go
func (h *AuthHandler) setTokenCookies(w http.ResponseWriter, accessToken, refreshToken string, atExp, rtExp time.Time) {
    secure   := !h.isDevelopment
    sameSite := http.SameSiteLaxMode
    if !h.isDevelopment { sameSite = http.SameSiteNoneMode }

    http.SetCookie(w, &http.Cookie{
        Name: "accessToken", Value: accessToken,
        Path: "/", MaxAge: int(time.Until(atExp).Seconds()),
        HttpOnly: true, Secure: secure, SameSite: sameSite,
    })
    http.SetCookie(w, &http.Cookie{
        Name: "refreshToken", Value: refreshToken,
        Path: "/", MaxAge: int(time.Until(rtExp).Seconds()),
        HttpOnly: true, Secure: secure, SameSite: sameSite,
    })
}
```

---

## 9. Testing Strategy

### Application Layer: Unit Tests

Mirrors `Auth.Application.UnitTest` — handlers tested in isolation with mock interfaces via `testify/mock`.

```go
// internal/application/command/register/handler_test.go
// Unit tests use sqlmock (github.com/DATA-DOG/go-sqlmock) to mock the DB driver,
// so *sqlc.Queries can be constructed without a real SQL Server connection.
func TestRegisterHandler_Success(t *testing.T) {
    db, mock, _ := sqlmock.New()
    defer db.Close()
    mock.ExpectExec("INSERT INTO Users").WillReturnResult(sqlmock.NewResult(1, 1))

    dispatcher := &MockDispatcher{}
    dispatcher.On("Dispatch", mock.Anything, mock.AnythingOfType("event.RegisterEvent")).Return(nil)

    h := register.NewHandler(db.New(conn), dispatcher, slog.Default(), "test-secret")
    result, err := h.Handle(context.Background(), register.Command{
        Email: "test@example.com", Password: "P@ssw0rd1!", FirstName: "First", LastName: "Last",
    })

    assert.NoError(t, err)
    assert.NotEmpty(t, result.UserId)
}

func TestRegisterHandler_ValidationFails(t *testing.T) {
    h := register.NewHandler(nil, nil, slog.Default(), "test-secret")
    _, err := h.Handle(context.Background(), register.Command{Email: "not-an-email"})

    var ve *apperror.ValidationErrors
    assert.ErrorAs(t, err, &ve)
}
```

### Infrastructure Layer: Integration Tests

Mirrors `Auth.Infrastructure.IntegrationTests`. MSSQL has no in-memory option, so infrastructure integration tests use **`testcontainers-go`** to spin up a real SQL Server container (requires Docker). Mark these tests with the `integration` build tag so they are excluded from the default `go test ./...` run.

```go
//go:build integration

// internal/application/command/register/handler_integration_test.go
package register_test

import (
    "context"
    "fmt"
    "testing"

    "github.com/stretchr/testify/assert"
    "github.com/stretchr/testify/require"
    "github.com/testcontainers/testcontainers-go"
    "github.com/testcontainers/testcontainers-go/wait"

    db "auth/internal/application/db/sqlc"
    "auth/internal/infrastructure/persistence"
)

func setupTestDB(t *testing.T) *db.Queries {
    t.Helper()
    ctx := context.Background()

    req := testcontainers.ContainerRequest{
        Image:        "mcr.microsoft.com/mssql/server:2022-latest",
        ExposedPorts: []string{"1433/tcp"},
        Env: map[string]string{
            "ACCEPT_EULA":       "Y",
            "MSSQL_SA_PASSWORD": "Test@1234",
        },
        WaitingFor: wait.ForListeningPort("1433/tcp"),
    }
    container, err := testcontainers.GenericContainer(ctx,
        testcontainers.GenericContainerRequest{ContainerRequest: req, Started: true})
    require.NoError(t, err)
    t.Cleanup(func() { container.Terminate(ctx) })

    host, _ := container.Host(ctx)
    port, _ := container.MappedPort(ctx, "1433")
    dsn := fmt.Sprintf("sqlserver://sa:Test@1234@%s:%s?database=master", host, port.Port())

    queries, err := persistence.NewDB(ctx, dsn)
    require.NoError(t, err)
    return queries
}

func TestRegister_ConflictOnDuplicate(t *testing.T) {
    queries     := setupTestDB(t)
    dispatcher  := &MockDispatcher{}
    dispatcher.On("Dispatch", mock.Anything, mock.Anything).Return(nil)
    h := register.NewHandler(queries, dispatcher, slog.Default(), "test-secret")

    _, err := h.Handle(context.Background(), register.Command{
        Email: "dup@example.com", Password: "P@ssw0rd1!", FirstName: "A", LastName: "B",
    })
    require.NoError(t, err)

    _, err = h.Handle(context.Background(), register.Command{
        Email: "dup@example.com", Password: "P@ssw0rd1!", FirstName: "A", LastName: "B",
    })

    var appErr *apperror.AppError
    assert.ErrorAs(t, err, &appErr)
    assert.Equal(t, 409, appErr.Code)
}
```

Application-layer unit tests (mock interfaces) and HTTP end-to-end tests (`httptest`) are unchanged — they never touch the database.

### HTTP Layer: End-to-End Tests

Uses the standard `net/http/httptest` package — works identically with `net/http` `ServeMux`:

```go
func TestLogin_Returns200_WithCookies(t *testing.T) {
    mux := setupTestMux() // wires real handlers against in-memory DB
    w   := httptest.NewRecorder()
    req := httptest.NewRequest("POST", "/api/auth/login",
        strings.NewReader(`{"email":"test@example.com","password":"P@ssw0rd1!"}`))
    req.Header.Set("Content-Type", "application/json")

    mux.ServeHTTP(w, req)

    assert.Equal(t, http.StatusOK, w.Code)
    assert.True(t, slices.ContainsFunc(w.Result().Cookies(), func(c *http.Cookie) bool {
        return c.Name == "accessToken"
    }))
}
```

### Run Commands

```bash
# Regenerate sqlc Go code after editing any .sql file in internal/application/
sqlc generate -f internal/application/db/sqlc.yaml

# Run migrations manually (also runs automatically at startup)
goose -dir internal/application/db/migrations mssql "sqlserver://user:pass@host:1433?database=auth" up

go test ./internal/application/...                              # Unit tests (handlers + validators)
go test -tags integration ./internal/application/...           # Integration tests (requires Docker)
go test ./internal/transport/...                               # HTTP end-to-end tests
go test ./...                                                  # All non-integration tests
```

---

## 10. .NET vs Go Equivalents

| .NET (src/Auth) | Go (golang/auth) | Notes |
|---|---|---|
| `Auth.Application` project | `internal/application/` package | Go uses packages instead of projects |
| `Auth.Infrastructure` project | `internal/infrastructure/` package | |
| `IRequest<T>` (MediatR) | `Command`/`Query` struct | No generic marker interface needed |
| `IRequestHandler<TReq, TRes>` | Local `Handler` interface per package | Small, explicit, Go-idiomatic |
| `ISender.Send(command)` | Direct: `h.loginHandler.Handle(ctx, cmd)` | No runtime dispatch / reflection |
| `IPublisher.Publish(event)` | `dispatcher.Dispatch(ctx, event)` | Concurrent fan-out via `sync.WaitGroup` |
| `INotificationHandler<TEvent>` | Concrete struct with `Handle(ctx, Event)` | Registered in dispatcher at wiring time |
| `AbstractValidator<T>` (FluentValidation) | Struct tags + `validator.Validate` + custom funcs | `go-playground/validator/v10` |
| `IPipelineBehavior<,>` (MediatR pipeline) | `decorator.ValidatingDecorator[C, R]` for command-level; `func(http.Handler) http.Handler` for HTTP-level | Command decorators applied at wiring time in `cmd/main.go`; HTTP middleware applied in `server.go` |
| `ValidationBehavior` | `decorator.NewValidating[C, R](inner, v)` wrapping each handler | Decorator applied at wiring time — handlers contain no validation logic |
| `UnhandledExceptionBehavior` | `middleware.Recovery()` | Panic recovery via `http.Handler` wrapper |
| `PerformanceBehavior` | `middleware.Performance()` | Logs requests > 500ms as Warning |
| `ExceptionMiddleware` | `writeError()` in `AppHandlerFunc` | Maps returned errors to HTTP status codes |
| `IIdentityService` | _(removed)_ — each handler calls `*sqlc.Queries` directly | No shared abstraction needed; each command owns its DB logic |
| `IJwtProvider` | `port.JwtProvider` interface | Outbound port |
| `IAuthDbContext` | `*sqlc.Queries` passed directly to handlers | No repository interface — each handler owns its DB logic in one place |
| `ICurrentUser` | `port.CurrentUser` interface | Extracted from JWT claims in middleware |
| `ApplicationUser : IdentityUser` | `db.User` (sqlc-generated struct in `db/sqlc/`) | No domain entity needed; handlers work directly with sqlc structs and map to DTOs inline |
| `UserManager` / `IdentityService` | `handler.createUser()` and equivalent per-handler methods | Each command handler owns its DB interaction directly via `*sqlc.Queries`; no shared IdentityService intermediary |
| `JwtProvider` | `identity.jwtProvider` | `golang-jwt/jwt/v5`; same dual-secret design |
| `JwtBearerSetup` | `middleware.Auth()` | Reads `accessToken` cookie, validates JWT, stores user in `context.Context` |
| `AuthDbContext : IdentityDbContext` | `persistence.NewDB()` returning `*db.Queries` + goose migrations | MSSQL via `go-mssqldb`; SQL-first; generated code in `application/db/sqlc/`, owned by the application layer |
| `IEntityTypeConfiguration<T>` | `application/db/migrations/*.sql` (goose) + per-feature `.sql` files in `internal/application/` (sqlc source) | Explicit SQL, version-controlled; sqlc generates type-safe Go into `application/db/sqlc/` |
| `SendRegisterConfirmEmail : INotificationHandler` | `mailer.SendRegisterConfirmEmail` | Called by dispatcher |
| `DependencyInjection.cs` | `cmd/main.go` wiring block | Manual constructor injection |
| `BadRequestException` | `apperror.AppError{Code: 400}` | Defined in `internal/application/apperror/` |
| `NotFoundException` | `apperror.AppError{Code: 404}` | Defined in `internal/application/apperror/` |
| `ConflictException` | `apperror.AppError{Code: 409}` | Defined in `internal/application/apperror/` |
| `UnprocessableEntityException` | `apperror.ValidationErrors` | Defined in `internal/application/apperror/` |
| `AutoMapper` | Inline mapping inside each handler from `sqlc.*` structs to DTOs | No shared mapper layer; each handler maps only what it needs |
| `ILogTrace` | `log/slog` structured logging | Standard library since Go 1.21 |
| `CancellationToken` | `context.Context` (first param everywhere) | Go's idiomatic cancellation |
| `async/await` | Blocking calls (`database/sql` is synchronous) | Goroutines used only for concurrent event dispatch |
| `TaskWhenAllPublisher` | `sync.WaitGroup` fan-out in dispatcher | Event handlers run concurrently |
| `Moq` / `Shouldly` | `testify/mock` + `testify/assert` | |

---

## Key Design Decisions

**Why PBKDF2 instead of bcrypt?**
Go and .NET share the same `Users` table. ASP.NET Identity V3 stores passwords as PBKDF2-HMAC-SHA256 (10 000 iterations, 16-byte salt, 32-byte key), base64-encoded with a specific 61-byte layout. A user registered via .NET must be able to log in via Go and vice versa. Using bcrypt would produce incompatible hashes. The `golang.org/x/crypto/pbkdf2` package replicates the exact format; see `hashPasswordIdentityV3` and `verifyPasswordIdentityV3` in `internal/application/command/register/handler.go`.

**Why HMAC-SHA256 tokens instead of random tokens stored in the database?**
.NET's `DataProtectorTokenProvider` issues stateless, self-contained tokens that encode the user's `SecurityStamp` + expiry and are signed with a machine key — no token table exists in the DB. The Go implementation mirrors this approach using HMAC-SHA256 signed with `TOKEN_SECRET`. Tokens are automatically invalidated when `SecurityStamp` changes (logout, password change). No goose migration is needed for the Register or ConfirmEmail features.

**Why `net/http` instead of Gin?**
Go 1.22 added method-prefixed routing (`POST /api/auth/login`) and path parameters (`{id}`) directly to `http.ServeMux`, eliminating the primary reason to reach for Gin. Standard `func(http.Handler) http.Handler` middleware is universally composable, testable with `httptest.NewRecorder()` without any framework setup, and works identically across the ecosystem. Zero external dependencies, no magic context types, no framework-specific handler signatures.

**Why no mediator bus?**
Go's structural interfaces are already lightweight. Having `loginHandler login.Handler` as a named field in the HTTP struct has zero coupling penalty and is immediately readable. MediatR adds reflection overhead that Go simply does not need.

**Why sqlc instead of an ORM?**
sqlc generates type-safe Go code directly from hand-written SQL. There is no runtime reflection, no "magic" query building, and no hidden N+1 risk. Every query is visible, reviewable in PRs, and validated at code-generation time. This aligns with Go's philosophy: explicit over implicit. The generated structs in `internal/application/db/sqlc/` are ordinary Go structs — no ORM tags, no framework coupling.

**Why goose instead of AutoMigrate?**
`AutoMigrate` generates SQL at runtime based on struct tags — the actual SQL it runs is invisible. goose uses explicit `.sql` files checked into the repository: reviewable, deterministic, and identical between development and production. Using `go:embed`, the migrations ship inside the binary with zero external file dependencies. For MSSQL, the `-- +goose StatementBegin/End` wrappers handle T-SQL batch boundaries correctly.

**Why no repository layer?**
A repository adds an indirection layer that splits one use case's logic across two files: the handler decides _what_ to do, the repository decides _how_ to do it. In practice, the "how" for auth operations is always "call sqlc" — so the layer adds complexity with no benefit. Each handler receives `*sqlc.Queries` directly and owns the full DB interaction in one place. This keeps each use case self-contained and easy to follow.

**Why `log/slog` instead of a third-party logger?**
Standard library since Go 1.21. Structured JSON output, log levels, context propagation — everything `ILogTrace` provides, with no extra dependency.

**Cookie strategy matches .NET exactly.**
`HttpOnly: true`, `Secure: !isDevelopment`, `SameSite: Lax` in development, `None` in production. Token rotation in `RefreshTokenHandler` follows the same delete-old/add-new pattern as `RefreshTokenCommandHandler.cs`.
