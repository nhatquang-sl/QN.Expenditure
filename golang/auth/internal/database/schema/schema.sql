-- Snapshot of .NET EF Core tables — for sqlc type inference only.
-- This file is NEVER applied to the database.

CREATE TABLE "Users" (
    "Id"                   text        NOT NULL PRIMARY KEY,
    "UserName"             text        NOT NULL,
    "NormalizedUserName"   text        NOT NULL,
    "Email"                text        NOT NULL,
    "NormalizedEmail"      text        NOT NULL,
    "EmailConfirmed"       boolean     NOT NULL DEFAULT false,
    "PasswordHash"         text        NOT NULL,
    "SecurityStamp"        text        NOT NULL,
    "ConcurrencyStamp"     text        NOT NULL,
    "PhoneNumber"          text,
    "PhoneNumberConfirmed" boolean     NOT NULL DEFAULT false,
    "TwoFactorEnabled"     boolean     NOT NULL DEFAULT false,
    "LockoutEnd"           timestamptz,
    "LockoutEnabled"       boolean     NOT NULL DEFAULT false,
    "AccessFailedCount"    integer     NOT NULL DEFAULT 0,
    "FirstName"            text        NOT NULL,
    "LastName"             text        NOT NULL
);

CREATE TABLE "UserSessions" (
    "Id"           bigserial   PRIMARY KEY,
    "UserId"       text        NOT NULL,
    "IpAddress"    text        NOT NULL DEFAULT '',
    "UserAgent"    text        NOT NULL DEFAULT '',
    "AccessToken"  text        NOT NULL,
    "RefreshToken" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL,
    "RememberMe"   boolean     NOT NULL DEFAULT false
);

CREATE TABLE "UserSessionHistories" (
    "Id"           bigserial   PRIMARY KEY,
    "SessionId"    bigint      NOT NULL,
    "UserId"       text        NOT NULL,
    "IpAddress"    text        NOT NULL DEFAULT '',
    "UserAgent"    text        NOT NULL DEFAULT '',
    "AccessToken"  text        NOT NULL,
    "RefreshToken" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL,
    "RememberMe"   boolean     NOT NULL DEFAULT false
);

CREATE TABLE "EmailType" (
    "Id"           text        PRIMARY KEY,
    "Subject"      text        NOT NULL,
    "HtmlTemplate" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL DEFAULT NOW()
);

CREATE TYPE email_queue_status AS ENUM ('new', 'sending', 'sent', 'fail');

CREATE TABLE "EmailQueue" (
    "Id"          bigserial          PRIMARY KEY,
    "EmailTypeId" text               NOT NULL,
    "HtmlData"    text               NOT NULL,
    "Status"      email_queue_status NOT NULL DEFAULT 'new',
    "Retry"       integer            NOT NULL DEFAULT 0,
    "NextRetryAt" timestamptz,
    "UserId"      text               NOT NULL,
    "CreatedAt"   timestamptz        NOT NULL DEFAULT NOW(),
    "UpdatedAt"   timestamptz        NOT NULL DEFAULT NOW()
);

CREATE TABLE "Roles" (
    "Id"        varchar(20) PRIMARY KEY,
    "Name"      varchar(50) NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT NOW()
);

CREATE TABLE "UserRoles" (
    "UserId"    text        NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "RoleId"    varchar(20) NOT NULL REFERENCES "Roles"("Id") ON DELETE RESTRICT,
    "CreatedAt" timestamptz NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("UserId", "RoleId")
);
