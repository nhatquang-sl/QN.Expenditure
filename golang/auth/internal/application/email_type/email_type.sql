-- name: GetEmailTypeById :one
SELECT "Id", "Subject", "HtmlTemplate", "CreatedAt"
FROM "EmailType"
WHERE "Id" = $1;
