CREATE TABLE IF NOT EXISTS "Users" (
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
