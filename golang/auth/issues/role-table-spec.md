# Spec: Role Table with Default User Role on Registration

## Problem Statement

The auth service has no concept of roles. Every authenticated user is treated identically, with no way to distinguish between regular users and administrators. Downstream services cannot make role-based access decisions from the JWT token alone, and the profile endpoint exposes no role information.

## Solution

Introduce a `Roles` table seeded with two fixed roles (`admin` and `user`), a `UserRoles` join table to associate users with roles, and automatically assign the `user` role to every newly registered account. Embed roles in the JWT access token claims so downstream services can enforce access control without additional database lookups. Surface roles in the `GET /profile` response.

## User Stories

1. As a newly registered user, I want to be automatically assigned the `user` role, so that I do not need to take any additional action to access user-level features.
2. As an API consumer, I want the JWT access token to contain the user's roles, so that I can enforce role-based access control without making extra database calls.
3. As an authenticated user, I want `GET /profile` to include my roles, so that the frontend can display role-appropriate UI without decoding the JWT.
4. As an administrator, I want a predefined `admin` role to exist in the database from the first migration, so that it can be assigned to users manually or via future tooling.
5. As a developer, I want role assignment to happen in the register handler — not a database trigger — so that the business logic is visible, testable, and consistent with the rest of the application layer.
6. As a developer, I want the `Roles` table to use a text primary key (e.g. `'admin'`, `'user'`), so that role identifiers are self-documenting in queries and logs without needing a lookup table join.
7. As a developer, I want `UserRoles` to use a composite primary key `(UserId, RoleId)`, so that the uniqueness constraint is enforced at the schema level with no surrogate key overhead.
8. As a developer, I want `UserRoles.UserId` to cascade on delete, so that removing a user automatically removes their role assignments.
9. As a developer, I want `UserRoles.RoleId` to restrict on delete, so that a role cannot be accidentally dropped while users still hold it.
10. As a developer, I want `UserRoles.CreatedAt` to be recorded automatically, so that we have an audit trail of when each role was assigned.

## Implementation Decisions

- **Schema — `Roles` table**: `Id varchar(20)` primary key (role slugs are short identifiers; 20 characters is sufficient for any realistic slug and provides a tighter B-tree index bound), `Name varchar(50)` (generous for any display name; e.g. "Admin", "Content Moderator"), and `CreatedAt timestamptz NOT NULL DEFAULT NOW()`. Seeded with `('admin', 'Admin')` and `('user', 'User')` in the migration. Consistent with the existing `EmailType` table which uses the same text-PK, seeded pattern.

- **Schema — `UserRoles` join table**: Columns `UserId text`, `RoleId varchar(20)`, `CreatedAt timestamptz NOT NULL DEFAULT NOW()`. Composite primary key `(UserId, RoleId)`. Foreign keys: `UserId REFERENCES Users(Id) ON DELETE CASCADE`, `RoleId REFERENCES Roles(Id) ON DELETE RESTRICT`.

- **Migration**: A single new migration (`000006_create_roles`) creates both `Roles` and `UserRoles` and seeds the two roles. The down migration drops `UserRoles` first, then `Roles`.

- **SQL queries**: Three new sqlc-annotated queries are needed:
  - `AssignUserRole :exec` — insert into `UserRoles` (used by the register handler)
  - `GetUserRoles :many` — select `RoleId` from `UserRoles` by `UserId` (used by the login and get_profile handlers)

- **Register handler**: After `CreateUser` succeeds, immediately call `AssignUserRole` with the new user's ID and `RoleId = 'user'`. This keeps role assignment explicit and visible in business logic.

- **JWT claims — `UserClaims`**: Add `Roles []string` to the shared `UserClaims` struct. The internal `authClaims` struct in the JWT service also gains a `Roles` field. `GenerateTokens` reads `Roles` from the provided `UserClaims`; `ValidateAccessToken` extracts `Roles` back out.

- **Login handler**: After creating the user session, fetch the user's roles via `GetUserRoles`, populate `UserClaims.Roles`, then call `GenerateTokens`. Roles are embedded in **both** the access token and the refresh token.

- **Refresh token handler**: Carries roles forward from `claims.Roles` (extracted from the refresh token) — no DB query needed on refresh. When roles or profile data change in the future, a Redis staleness flag (e.g. `stale-claims:<userId>`) will signal the handler to re-fetch from the DB instead of carrying forward, then clear the flag. This pattern keeps the common path (no change) free of DB hits while still allowing immediate propagation of role changes.

- **Get profile handler**: After `GetUserProfileById`, call `GetUserRoles` and include the result as `Roles []string` in the `Result` struct. Since the profile is cached in Redis, roles are included in the cached value — they are stable and rarely change.

- **Out-of-scope for now**: No API endpoint to assign or remove roles. Admin role assignment is done directly in the database.

## Testing Decisions

**What makes a good test here**: Test only external observable behavior — request in, response out for HTTP tests; input/output for unit tests. Do not assert on internal state, SQL queries, or handler implementation details.

### Unit tests — `internal/services/jwt/service_test.go` (new file)

The JWT service is pure logic with no external dependencies (no DB, no HTTP). It is the right target for unit tests that verify the `Roles` field round-trips correctly through token generation and validation.

**Prior art**: No unit tests exist in `internal/` yet — this is the first. The JWT service takes a secret string and a `UserClaims` struct; both can be constructed inline with no mocks.

Tests:
- `TestGenerateTokens/RolesRoundTrip` — call `GenerateTokens` with `UserClaims{Roles: []string{"user"}}`, then `ValidateAccessToken` on the result, assert `claims.Roles == []string{"user"}`.
- `TestGenerateTokens/EmptyRoles` — empty `Roles` slice round-trips without error and returns an empty (not nil) slice.
- `TestGenerateTokens/MultipleRoles` — `[]string{"admin", "user"}` round-trips correctly.

### Integration tests — `cmd/controller_tests/` (extend existing files)

**Single testing seam**: `newTestHandler().ServeHTTP(w, req)` — the full HTTP stack through real controllers, real application handlers, and a real PostgreSQL container. This is the only seam used throughout the existing test suite and should remain so.

**Prior art**: The `loginSuccess` test already uses `testJwtService.ValidateRefreshToken` to inspect JWT claims — the same approach applies for asserting `roles` in the access token. The `getProfileSuccess` test decodes the response body into `getprofile.Result` — adding `Roles` to that struct and asserting on it is the natural extension.

Tests:
- `TestRegister/AssignsUserRole` — register a user, log in, validate the access token via `testJwtService.ValidateAccessToken`, assert `claims.Roles == []string{"user"}`.
- `TestLogin/TokenContainsRoles` — validate the access token after login, assert `claims.Roles` contains `"user"`.
- `TestGetProfile/IncludesRoles` — register + login + `GET /profile`, assert response body contains `"roles": ["user"]`.

## Vertical Slices

Each slice is independently buildable and testable. Complete them in order — each one compiles and passes tests before the next begins.

| # | Slice | Deliverable | Verify |
|---|---|---|---|
| 1 | **Schema** | Migration `000006`, `schema.sql` update, `sqlc generate` | `make migrate-up` + `sqlc generate` succeed |
| 2 | **JWT roles** | `Roles []string` in `UserClaims` + `authClaims`, `GenerateTokens`/`ValidateAccessToken` updated, unit tests | `make test` passes `TestGenerateTokens/*` |
| 3 | **Register assigns role** | `AssignUserRole` SQL query, register handler calls it after `CreateUser`, integration test | `TestRegister/AssignsUserRole` passes |
| 4 | **Login embeds roles** | `GetUserRoles` SQL query (login), login handler fetches + passes to JWT, integration test | `TestLogin/TokenContainsRoles` passes |
| 5 | **Profile returns roles** | `GetUserRoles` SQL query (get_profile), handler fetches + returns roles, integration test | `TestGetProfile/IncludesRoles` passes |

## Out of Scope

- An API endpoint to assign, revoke, or list roles.
- Role-based middleware or route guards within this service.
- Promoting a user to `admin` via any automated flow — this is done directly in the database.
- Multiple roles per user (the schema supports it, but no use case exists yet).

## Database Column Sizing Rule

Every column in this codebase must have an explicit size limit — `text` (unlimited) is never acceptable for columns that represent user-controlled or bounded data. Use `varchar(n)` with a realistic upper bound chosen per domain:

| Column type | Recommended limit | Rationale |
|---|---|---|
| Role slug / short identifier | `varchar(20)` | Slugs are terse machine identifiers |
| Display name / label | `varchar(50)` | Human-readable names rarely exceed 50 chars |
| Email address | `varchar(254)` | RFC 5321 maximum |
| URL / redirect URI | `varchar(2048)` | Browser/server practical limit |
| Free-form short text | `varchar(255)` | Classic safe default |
| Long-form content (templates, HTML) | `text` | Only acceptable for genuinely unbounded content |

This rule applies to all future migrations and schema changes.

## Further Notes

- The `Roles` table is intentionally static. Do not add a CRUD API around it — the two fixed roles are an application-level invariant, not user-managed data.
- The `GetUserRoles` query is needed independently in both the login and get_profile SQL files because sqlc generates per-package query sets. Sharing is not possible without restructuring packages.
- After modifying any `.sql` files, run `sqlc generate -f internal/database/sqlc.yaml` before building.
