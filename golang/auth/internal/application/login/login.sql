-- name: GetUserByNormalizedEmail :one
SELECT "Id", "Email", "FirstName", "LastName", "EmailConfirmed", "PasswordHash"
FROM "Users"
WHERE "NormalizedEmail" = $1;

-- name: CreateUserSession :one
INSERT INTO "UserSessions" ("UserId", "IpAddress", "UserAgent", "AccessToken", "RefreshToken", "CreatedAt", "RememberMe")
VALUES ($1, $2, $3, $4, $5, $6, $7)
RETURNING "Id";
