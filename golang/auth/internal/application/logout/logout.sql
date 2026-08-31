-- name: DeleteUserSessionById :exec
DELETE FROM "UserSessions"
WHERE "Id" = $1;
