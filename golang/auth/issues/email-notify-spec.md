# Email Notification Feature Spec

**Label**: ready-for-agent

---

## Problem Statement

The auth service needs to send transactional emails to users (e.g. account activation, password reset) but currently has no reliable delivery mechanism. The existing `EmailService` interface is a stub — the register handler fires email in a goroutine with no retry, no audit trail, and no observability. Emails that fail to send are silently lost.

## Solution

Implement a full email pipeline with guaranteed delivery and retry:

1. Application code publishes an email job to RabbitMQ via a generic `EmailService.Send()` call.
2. A dedicated consumer binary reads from RabbitMQ and persists the job to an `EmailQueue` table in PostgreSQL.
3. A dedicated worker binary polls `EmailQueue`, renders the HTML template, and sends via Mailjet — with exponential backoff and a 3-attempt retry limit.

The DB queue is the source of truth for delivery status and auditability. Template content and subjects are stored in an `EmailType` table, seeded via migrations and version-controlled.

## User Stories

1. As a newly registered user, I want to receive an account activation email, so that I can confirm my email address and activate my account.
2. As a user, I want my activation email to arrive within 30 seconds of registration, so that the experience feels responsive.
3. As a user, I want the system to retry sending my email if delivery fails transiently, so that a temporary Mailjet outage does not prevent me from activating my account.
4. As a user, I want retried emails to use backoff intervals, so that I am not bombarded with duplicate sends in quick succession.
5. As a user, I want each email to be sent at most once, so that I do not receive duplicate activation emails.
6. As a developer, I want all email types (template, subject, content) to be version-controlled via migrations, so that changes to email content are auditable and reviewable.
7. As a developer, I want the data passed to each email type to be type-safe at compile time, so that mismatches between template variables and published data are caught before deployment.
8. As a developer, I want the email pipeline to degrade gracefully when RabbitMQ is unavailable, so that a MQ outage does not break user registration.
9. As a developer, I want the worker to process emails in configurable batch sizes, so that throughput can be tuned for production without a code change.
10. As a developer, I want the worker polling interval to be configurable, so that the trade-off between latency and DB load can be adjusted without a code change.
11. As an operator, I want each email attempt logged in the `EmailQueue` table with its status, retry count, and timestamps, so that I can audit delivery failures.
12. As an operator, I want permanently failed emails (after 3 attempts) to remain in `EmailQueue` with status `fail`, so that I can identify and investigate delivery issues.
13. As an operator, I want the consumer and worker to run as separate binaries, so that they can be deployed, scaled, and restarted independently.
14. As an operator, I want the worker to be safe to run as multiple instances simultaneously, so that I can scale it horizontally without duplicate sends.
15. As an operator, I want email templates stored in the database (not on disk), so that the worker binary has no file-system dependency.

## Implementation Decisions

### Pipeline architecture

The pipeline has three distinct hops:

- **Hop 1**: Application code → RabbitMQ (publish)
- **Hop 2**: RabbitMQ → PostgreSQL `EmailQueue` (consumer persists)
- **Hop 3**: PostgreSQL `EmailQueue` → Mailjet (worker sends)

The DB queue is intentional — it provides the auditability and retry durability that RabbitMQ alone cannot guarantee across restarts and failures.

### `EmailType` table schema

| Column | Type | Notes |
|---|---|---|
| `Id` | text PRIMARY KEY | Human-readable slug, e.g. `activate_account` |
| `Subject` | text NOT NULL | Mailjet email subject line |
| `HtmlTemplate` | text NOT NULL | Go `html/template` syntax |
| `CreatedAt` | timestamptz NOT NULL DEFAULT NOW() | |

Managed exclusively via golang-migrate seed migrations. No admin API.

### `EmailQueue` table schema

| Column | Type | Notes |
|---|---|---|
| `Id` | bigserial PRIMARY KEY | |
| `EmailTypeId` | text FK → EmailType.Id NOT NULL | |
| `HtmlData` | text NOT NULL | JSON-encoded template data |
| `Status` | email_queue_status NOT NULL DEFAULT 'new' | PostgreSQL enum: `new`, `sending`, `sent`, `fail` |
| `Retry` | int NOT NULL DEFAULT 0 | Max 3 attempts |
| `NextRetryAt` | timestamptz | NULL means eligible immediately |
| `UserId` | text FK → Users.Id NOT NULL | Recipient; always a known user |
| `CreatedAt` | timestamptz NOT NULL DEFAULT NOW() | |
| `UpdatedAt` | timestamptz NOT NULL DEFAULT NOW() | Updated on every status change |

A partial index `idx_email_queue_eligible` on `("CreatedAt") WHERE "Status" IN ('new', 'fail')` covers the worker's poll query.

### `EmailService` interface

The existing `shared.EmailService` interface is **replaced entirely** with a new generic interface:

```go
type EmailService interface {
    Send(ctx context.Context, userId, emailType string, data any) error
}
```

The implementation marshals `data` to JSON and publishes to RabbitMQ. If RabbitMQ is unavailable, the error is logged and the caller moves on — email is best-effort at the publish boundary.

The old `SendEmailConfirmation` method and its implementation are deleted. The register handler is updated to call the new `Send` method with the `activate_account` email type.

### Typed data contracts

A shared package contains one Go struct per email type. The struct fields must match the `html/template` variables in the corresponding `EmailType.HtmlTemplate`. This is a compile-time contract — no runtime schema validation.

Example:
```go
type ActivateAccountData struct {
    FirstName  string
    ConfirmURL string
}
```

Callers marshal the concrete struct and pass it as the `data` argument to `EmailService.Send()`.

### RabbitMQ topology

- **Exchange**: direct, named `email`, durable
- **Routing key**: `email.notify`
- **Queue**: named `email.queue`, durable
- **Acknowledgement**: manual — consumer acks only after a successful `EmailQueue` INSERT; nacks with requeue on DB failure

### Consumer binary (`cmd/consumer`)

- Long-running process subscribing to `email.queue`
- On each message: validate that `EmailTypeId` exists in `EmailType` table; if unknown, log a warning and ack (discard — this is a programming error, not a transient failure)
- On valid message: insert row into `EmailQueue` with `status = 'new'`, then ack

### Worker binary (`cmd/worker`)

- Separate binary from both the API server and the consumer
- Polls `EmailQueue` on a configurable interval (default 30 seconds)
- Uses `SELECT ... FOR UPDATE SKIP LOCKED` to safely support multiple concurrent instances
- Eligible rows: `status = 'new' OR (status = 'fail' AND retry < 3 AND nextRetryAt <= NOW())`
- Processes up to `BatchSize` rows per tick (default 50)
- Per row:
  1. Set `status = 'sending'`
  2. Render `html/template` from `EmailType.HtmlTemplate` + `EmailQueue.HtmlData`
  3. Send via Mailjet (from address from config)
  4. On success: set `status = 'sent'`
  5. On failure: increment `retry`, set `status = 'fail'`, compute `nextRetryAt = NOW() + interval * 2^retry` (exponential backoff starting at 1 minute); if `retry >= 3`, leave as `fail` permanently

### Config additions

```json
"EmailWorker": {
  "BatchSize": 50,
  "IntervalSeconds": 30
}
```

Added to `appsettings.json` and the `Config` struct as `EmailWorker`. Both binaries load the same config file.

### Mailjet integration

Uses the official Mailjet Go client. Credentials (`ApiKeyPublic`, `ApiKeyPrivate`, `FromEmail`) are already present in the `Email` section of `appsettings.json`. No changes to config loading required beyond the `EmailWorker` section.

### Register handler update

The `register` handler's `emailService.SendEmailConfirmation(...)` call is replaced with `emailService.Send(ctx, userId, "activate_account", ActivateAccountData{...})`. The goroutine wrapper is removed — fire-and-forget is now handled by the pipeline itself.

## Testing Decisions

A good test exercises observable external behavior, not internal implementation. For this feature:
- A good consumer test proves that a RabbitMQ message results in a row in `EmailQueue`
- A good worker test proves that an `EmailQueue` row in `new` status results in a Mailjet call and a `sent` status update

### Consumer tests (`cmd/consumer`)

- Use `testcontainers-go` to spin up both a PostgreSQL container and a RabbitMQ container, following the pattern in `cmd/controller_tests/main_test.go`
- Publish a message to the durable queue and assert the resulting `EmailQueue` row has the correct fields and `status = 'new'`
- Test that an unknown `emailType` results in no row inserted and a consumed (acked) message

### Worker tests (`cmd/worker`)

- Use `testcontainers-go` for PostgreSQL only
- Inject a fake `EmailSender` interface — no real Mailjet calls in CI
- Seed `EmailType` and `EmailQueue` rows directly; run one worker tick; assert status transitions and retry field values
- Test cases: successful send, transient failure with retry increment and `nextRetryAt` set, third failure resulting in permanent `fail`

### Integration: register flow

- Existing register tests in `cmd/controller_tests/register_test.go` already pass `nil` for `emailService` — no change needed
- Add a test that wires a mock `EmailService` and asserts `Send` is called with the correct `userId` and `emailType`

## Vertical Slices

Each slice is independently deployable and testable. Later slices depend on earlier ones but add no value until the full pipeline is complete — so slices 1–4 are infrastructure, slice 5 is the first user-visible delivery.

---

### Slice 1: DB schema + email type seed ✅ Done

**Delivers**: The `EmailType` and `EmailQueue` tables exist in PostgreSQL with the correct schema. The `activate_account` email type is seeded with a real HTML template and subject line.

**Work**:
- Migration 000003: create `EmailType` table
- Migration 000004: create `EmailQueue` table with `email_queue_status` PostgreSQL enum, partial index `idx_email_queue_eligible`, and `DEFAULT NOW()` on `UpdatedAt`
- Migration 000005: seed `activate_account` email type with subject and HTML template
- Add `EmailWorkerConfig { BatchSize, IntervalSeconds }` to `Config` struct
- SQL query files: `internal/application/email_type/email_type.sql`, `internal/application/email_queue/email_queue.sql`
- Run `sqlc generate` — produces typed queries and `EmailQueueStatus` Go enum constants

**Done when**: `make migrate-up` runs cleanly and `SELECT * FROM "EmailType"` returns the `activate_account` row.

---

### Slice 2: Typed data contracts + `EmailService` interface

**Delivers**: The compile-time contract between application code and the email pipeline. The register handler is updated — though email is not yet actually sent.

**Work**:
- Define typed data structs package (e.g. `ActivateAccountData`)
- Replace `shared.EmailService` interface with the new `Send(ctx, userId, emailType, data)` signature
- Implement a no-op `EmailService` (logs only) for local dev without RabbitMQ
- Update `register` handler: replace `SendEmailConfirmation` call with `Send`, remove goroutine wrapper
- Update `cmd/main.go` to wire the no-op implementation
- Update register tests to assert `Send` is called with the correct `userId` and `emailType`

**Done when**: all existing tests pass, the register handler compiles with the new interface, and the no-op implementation logs the correct payload.

---

### Slice 3: RabbitMQ publisher (`EmailService` real implementation)

**Delivers**: Application code successfully publishes email jobs to RabbitMQ. The pipeline's first hop is live.

**Work**:
- Implement the RabbitMQ `EmailService`: marshal data to JSON, declare durable exchange + queue, publish with routing key `email.notify`
- Wire the real implementation in `cmd/main.go` (behind a nil-guard so local dev without RabbitMQ still uses the no-op)
- Fail fast on publish error: log and return — no fallback

**Done when**: registering a user causes a message to appear in the `email.queue` RabbitMQ queue (verifiable via the RabbitMQ management UI).

---

### Slice 4: Consumer binary (`cmd/consumer`)

**Delivers**: RabbitMQ messages are reliably persisted to `EmailQueue`. The second hop is live. Emails are now durable across service restarts.

**Work**:
- Create `cmd/consumer/main.go`: connect to RabbitMQ, subscribe to `email.queue`
- On message: validate `EmailTypeId` exists in `EmailType`; unknown types are logged and acked (discarded)
- On valid message: INSERT into `EmailQueue` with `status = 'new'`, then ack; nack+requeue on DB failure
- Consumer integration tests: real PostgreSQL + real RabbitMQ containers via `testcontainers-go`
- Test cases: valid message → row inserted; unknown email type → no row, message acked

**Done when**: publishing a message to RabbitMQ results in a `new` row in `EmailQueue` within seconds.

---

### Slice 5: Worker binary (`cmd/worker`)

**Delivers**: Emails are actually sent via Mailjet. The full pipeline is live end-to-end. User story 1 is complete.

**Work**:
- Create `cmd/worker/main.go`: poll loop with configurable interval and batch size
- Query eligible rows using `SELECT ... FOR UPDATE SKIP LOCKED LIMIT :batchSize`
- Per row: set `sending` → render template → call Mailjet → set `sent` or apply retry/backoff/fail logic
- Exponential backoff: `nextRetryAt = NOW() + (2^retry) minutes`; permanent `fail` after retry = 3
- Worker integration tests: real PostgreSQL + fake `EmailSender` interface; assert status transitions and `NextRetryAt` values
- Test cases: success → `sent`; first failure → `fail` + `retry=1` + `NextRetryAt` set; third failure → `fail` + `retry=3` + no further processing

**Done when**: a row in `EmailQueue` with `status = 'new'` is sent via Mailjet and transitions to `sent` within one poll interval.

---

## Out of Scope

- A UI or admin API for managing `EmailType` records — templates are managed via migrations only
- Sending emails to non-registered users (e.g. invitation flows) — `UserId` is required and non-nullable
- Email unsubscribe / preference management
- Email open/click tracking
- Multiple from-addresses or from-names
- SMS or other notification channels
- Dead-letter queue alerting — monitoring of permanent `fail` rows is an operator concern

## Further Notes

- The `EmailQueue` table uses the same PascalCase quoted-identifier convention as `UserSessions` and `UserSessionHistories`
- The worker's `SELECT ... FOR UPDATE SKIP LOCKED` query must be generated via sqlc — do not write raw SQL in application code
- RabbitMQ connection details are already in `appsettings.json` under the `RabbitMq` key; the consumer and worker read from the same config file as the API server
- Template rendering failures (malformed `html/template` syntax in the DB) count as a worker failure and trigger the retry/backoff path — they will permanently fail after 3 attempts, which is the correct signal that a migration introduced a bad template
