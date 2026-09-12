-- Schema snapshot for the email DB — for sqlc type inference only.
-- This file is NEVER applied to the database; migrations in migrations/ are.

CREATE TYPE email_queue_status AS ENUM ('new', 'sending', 'sent', 'fail');

CREATE TABLE "EmailType" (
    "Id"           text        PRIMARY KEY,
    "Subject"      text        NOT NULL,
    "HtmlTemplate" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE "EmailQueue" (
    "Id"          bigserial          PRIMARY KEY,
    "EmailTypeId" text               NOT NULL REFERENCES "EmailType"("Id"),
    "HtmlData"    text               NOT NULL,
    "Status"      email_queue_status NOT NULL DEFAULT 'new',
    "Retry"       integer            NOT NULL DEFAULT 0,
    "NextRetryAt" timestamptz,
    "UserId"      text               NOT NULL,
    "ToEmail"     text               NOT NULL,
    "CreatedAt"   timestamptz        NOT NULL DEFAULT NOW(),
    "UpdatedAt"   timestamptz        NOT NULL DEFAULT NOW()
);
