CREATE TABLE IF NOT EXISTS "UserLoginHistories" (
    "Id"           bigserial   PRIMARY KEY,
    "UserId"       text        NOT NULL,
    "IpAddress"    text        NOT NULL DEFAULT '',
    "UserAgent"    text        NOT NULL DEFAULT '',
    "AccessToken"  text        NOT NULL,
    "RefreshToken" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL,
    "RememberMe"   boolean     NOT NULL DEFAULT false
);
