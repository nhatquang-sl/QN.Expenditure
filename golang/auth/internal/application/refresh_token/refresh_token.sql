-- name: GetLoginHistoryByRefreshToken :one
SELECT "Id", "RememberMe"
FROM "UserLoginHistories"
WHERE "RefreshToken" = $1
LIMIT 1;

-- name: UpdateLoginHistoryTokens :exec
UPDATE "UserLoginHistories"
SET "AccessToken" = $2, "RefreshToken" = $3
WHERE "Id" = $1;
