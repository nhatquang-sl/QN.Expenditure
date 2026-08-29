-- name: GetLoginHistoryById :one
SELECT "Id", "RememberMe"
FROM "UserLoginHistories"
WHERE "Id" = $1;

-- name: UpdateLoginHistoryTokens :exec
UPDATE "UserLoginHistories"
SET "AccessToken" = $2, "RefreshToken" = $3
WHERE "Id" = $1;
