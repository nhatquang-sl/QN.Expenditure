-- name: DeleteLoginHistoryById :exec
DELETE FROM "UserLoginHistories"
WHERE "Id" = $1;
