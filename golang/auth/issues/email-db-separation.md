# Email Database Separation

**Label**: architecture

---

## Problem Statement

`EmailType` and `EmailQueue` currently live in the auth database (`PGAuth`). These are email infrastructure concerns, not auth concerns. Running them in the auth DB creates two problems:

1. **Wrong ownership boundary** — the auth service owns schema it shouldn't care about. Any schema change to email tables requires a migration against the auth DB.
2. **Hidden cross-DB coupling** — the Worker resolves recipient addresses by calling `GetUserEmailById` against the auth DB. Moving `EmailQueue` to a separate DB without fixing this query just adds a second DB connection with no real isolation gained.

---

## Solution

Two changes together achieve full isolation:

### 1. Denormalize `ToEmail` at publish time

The API already knows the user's email when it publishes to RabbitMQ. Include it in the message payload:

```json
{ "userId": "...", "toEmail": "user@example.com", "emailType": "activate_account", "data": {...} }
```

The Consumer stores `ToEmail` in `EmailQueue`. The Worker reads it directly — no auth DB query needed.

### 2. Move `EmailType` + `EmailQueue` to a dedicated email DB (`PGEmail`)

The Worker owns the email DB schema and runs its own migrations. The Consumer connects to `PGEmail` only for INSERTs.

---

## Service ↔ DB Connection Matrix

| Service | Auth DB (`PGAuth`) | Email DB (`PGEmail`) |
|---|---|---|
| `auth-api` | ✅ reads/writes | ❌ |
| `email-consumer` | ❌ | ✅ INSERT into `EmailQueue` |
| `email-worker` | ❌ | ✅ full access |

The auth API publishes to RabbitMQ — it never touches the email DB directly.

---

## Schema Changes

### `EmailQueue` — add `ToEmail`, relax `UserId`

```sql
ALTER TABLE "EmailQueue"
  ADD COLUMN "ToEmail" text NOT NULL;
```

`UserId` stays as a plain `text` column for audit purposes but loses its FK reference to `Users` (since the email DB has no `Users` table). It becomes a soft reference — identity only, no referential integrity.

### `EmailType` — no schema change

Same schema, just moves to the email DB.

---

## RabbitMQ Message Payload Change

**Before**:
```go
type emailMessage struct {
    UserId    string          `json:"userId"`
    EmailType string          `json:"emailType"`
    Data      json.RawMessage `json:"data"`
}
```

**After**:
```go
type emailMessage struct {
    UserId    string          `json:"userId"`
    ToEmail   string          `json:"toEmail"`
    EmailType string          `json:"emailType"`
    Data      json.RawMessage `json:"data"`
}
```

The `EmailService.Send()` interface in `internal/application/shared/` must accept or resolve `toEmail` at call time. The register handler already has the user's email — pass it through.

---

## Config Additions

Add `PGEmail` to `appsettings.json` and the `Config` struct:

```json
"ConnectionStrings": {
  "PGAuth":  "...",
  "PGEmail": "postgres://..."
}
```

The Consumer and Worker load `PGEmail`. The API server loads `PGAuth` only.

---

## Migration Plan (Vertical Slices)

### Slice 1 — Add `ToEmail` to message + `EmailQueue` schema

**Scope**: auth DB migration + Consumer + API publish path
**Work**:
- Migration on auth DB: `ALTER TABLE "EmailQueue" ADD COLUMN "ToEmail" text NOT NULL`
- Update `emailMessage` struct in Consumer to include `ToEmail`
- Update `EmailService.Send()` signature to accept `toEmail`
- Update register handler to pass user email
- Update `insert_email_queue` Command + Handler to store `ToEmail`
- Regenerate sqlc

**Done when**: Consumer stores `ToEmail` in `EmailQueue` and Worker can read it.

---

### Slice 2 — Create email DB with its own migrations

**Scope**: new PostgreSQL database + migration files
**Work**:
- Create new migration set (separate from auth migrations) under `cmd/worker/migrations/` or a dedicated `email_db/migrations/` directory
- Migration 000001: create `email_queue_status` enum + `EmailType` table
- Migration 000002: create `EmailQueue` table (with `ToEmail`, no FK on `UserId`)
- Migration 000003: seed `activate_account` email type
- Add `PGEmail` to `Config` struct and `appsettings.json`
- Add `PGEmail` service in `docker-compose.yml` (separate PostgreSQL container or separate DB on the same instance)

**Done when**: `make migrate-email-up` (or equivalent) runs cleanly against the new DB.

---

### Slice 3 — Switch Consumer to email DB

**Scope**: `cmd/consumers/email_consumer/main.go` + Consumer integration tests
**Work**:
- Connect to `PGEmail` instead of `PGAuth`
- Remove auth DB connection from consumer binary
- Update consumer integration tests (`cmd/consumer_tests/`) to use the email DB container

**Done when**: Consumer binary has zero connection to auth DB.

---

### Slice 4 — Switch Worker to email DB, remove `GetUserEmailById`

**Scope**: `cmd/worker/main.go` + `internal/worker/worker.go` + Worker tests
**Work**:
- Connect to `PGEmail` instead of `PGAuth`
- Remove `GetUserEmailById` query — use `EmailQueue.ToEmail` directly
- Delete `internal/application/worker/worker.sql` (the `GetUserEmailById` query)
- Regenerate sqlc
- Update Worker tests to seed `ToEmail` in `EmailQueue` rows

**Done when**: Worker binary has zero connection to auth DB.

---

### Slice 5 — Remove `EmailType` + `EmailQueue` from auth DB

**Scope**: auth DB migrations + `schema.sql` snapshot
**Work**:
- New auth DB migration: `DROP TABLE "EmailQueue"; DROP TABLE "EmailType"; DROP TYPE email_queue_status;`
- Remove `EmailType` and `EmailQueue` from `internal/database/schema/schema.sql`
- Regenerate sqlc for auth DB (removes generated types for these tables)

**Done when**: auth DB has no email-related tables and all auth service tests pass.

---

## What Does Not Change

- RabbitMQ topology (`email` exchange, `email.queue` queue, `email.notify` routing key) — unchanged
- Mailjet integration — unchanged
- Worker retry/backoff logic — unchanged
- `EmailType` schema and seed data — same content, different DB
- Consumer binary structure — same pattern, different DB connection

---

## Out of Scope

- Moving the email DB to a physically separate host (same Postgres instance with a separate DB is sufficient for now)
- An admin API for managing `EmailType` records — still managed via migrations only
- Any changes to the HTTP API surface

---

## Further Notes

- `UserId` in `EmailQueue` becomes a soft audit field — no referential integrity, just a record of which user triggered the email. This is acceptable since the email pipeline is fire-and-forget from the auth service's perspective.
- The Worker's `SELECT ... FOR UPDATE SKIP LOCKED` behaviour is unchanged — horizontal scaling safety is unaffected.
- Both the Consumer and Worker must be restarted after the email DB is provisioned. Order: provision DB → run migrations → start Worker → start Consumer.
