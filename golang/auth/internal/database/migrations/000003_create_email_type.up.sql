CREATE TABLE IF NOT EXISTS "EmailType" (
    "Id"           text        PRIMARY KEY,
    "Subject"      text        NOT NULL,
    "HtmlTemplate" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL DEFAULT NOW()
);
