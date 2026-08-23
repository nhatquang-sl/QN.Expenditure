-- name: GetUserProfileById :one
SELECT "Id", "Email", "FirstName", "LastName", "EmailConfirmed"
FROM "Users"
WHERE "Id" = $1;
