-- name: GetUserSessionById :one
SELECT "Id", "RememberMe"
FROM "UserSessions"
WHERE "Id" = $1;

-- name: UpdateUserSessionTokens :exec
UPDATE "UserSessions"
SET "AccessToken" = $2, "RefreshToken" = $3
WHERE "Id" = $1;
