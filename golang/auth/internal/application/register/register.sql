-- name: UserExistsByNormalizedEmail :one
SELECT EXISTS(
    SELECT 1 FROM "Users" WHERE "NormalizedEmail" = $1
) AS "exists";

-- name: CreateUser :exec
INSERT INTO "Users" (
    "Id", "UserName", "NormalizedUserName",
    "Email", "NormalizedEmail",
    "EmailConfirmed", "PasswordHash",
    "SecurityStamp", "ConcurrencyStamp",
    "FirstName", "LastName"
) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11);
