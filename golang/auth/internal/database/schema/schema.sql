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
