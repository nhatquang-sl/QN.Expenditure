-- name: ConfirmUserEmail :exec
UPDATE "Users" SET "EmailConfirmed" = true WHERE "Id" = $1;
