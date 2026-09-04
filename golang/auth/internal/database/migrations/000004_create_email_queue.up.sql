CREATE TYPE email_queue_status AS ENUM ('new', 'sending', 'sent', 'fail');

CREATE TABLE IF NOT EXISTS "EmailQueue" (
    "Id"          bigserial          PRIMARY KEY,
    "EmailTypeId" text               NOT NULL REFERENCES "EmailType"("Id"),
    "HtmlData"    text               NOT NULL,
    "Status"      email_queue_status NOT NULL DEFAULT 'new',
    "Retry"       integer            NOT NULL DEFAULT 0,
    "NextRetryAt" timestamptz,
    "UserId"      text               NOT NULL REFERENCES "Users"("Id"),
    "CreatedAt"   timestamptz        NOT NULL DEFAULT NOW(),
    "UpdatedAt"   timestamptz        NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_email_queue_eligible
    ON "EmailQueue" ("CreatedAt")
    WHERE "Status" IN ('new', 'fail');
