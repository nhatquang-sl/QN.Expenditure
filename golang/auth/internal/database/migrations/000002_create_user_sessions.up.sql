CREATE TABLE IF NOT EXISTS "UserSessions" (
    "Id"           bigserial   PRIMARY KEY,
    "UserId"       text        NOT NULL,
    "IpAddress"    text        NOT NULL DEFAULT '',
    "UserAgent"    text        NOT NULL DEFAULT '',
    "AccessToken"  text        NOT NULL,
    "RefreshToken" text        NOT NULL,
    "CreatedAt"    timestamptz NOT NULL,
    "RememberMe"   boolean     NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS "UserSessionHistories" (
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

CREATE OR REPLACE FUNCTION fn_user_sessions_insert_history()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO "UserSessionHistories" (
        "SessionId", "UserId", "IpAddress", "UserAgent",
        "AccessToken", "RefreshToken", "CreatedAt", "RememberMe"
    ) VALUES (
        NEW."Id", NEW."UserId", NEW."IpAddress", NEW."UserAgent",
        NEW."AccessToken", NEW."RefreshToken", NEW."CreatedAt", NEW."RememberMe"
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_user_sessions_insert_history
AFTER INSERT ON "UserSessions"
FOR EACH ROW EXECUTE FUNCTION fn_user_sessions_insert_history();
