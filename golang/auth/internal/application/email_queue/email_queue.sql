-- name: InsertEmailQueue :one
INSERT INTO "EmailQueue" ("EmailTypeId", "HtmlData", "UserId")
VALUES ($1, $2, $3)
RETURNING "Id", "EmailTypeId", "HtmlData", "Status", "Retry", "NextRetryAt", "UserId", "CreatedAt", "UpdatedAt";

-- name: GetEligibleEmailQueueBatch :many
SELECT "Id", "EmailTypeId", "HtmlData", "Status", "Retry", "NextRetryAt", "UserId", "CreatedAt", "UpdatedAt"
FROM "EmailQueue"
WHERE "Status" = 'new'
   OR ("Status" = 'fail' AND "Retry" < 3 AND "NextRetryAt" <= NOW())
ORDER BY "CreatedAt"
LIMIT $1
FOR UPDATE SKIP LOCKED;

-- name: UpdateEmailQueueSending :exec
UPDATE "EmailQueue"
SET "Status" = 'sending', "UpdatedAt" = NOW()
WHERE "Id" = $1;

-- name: UpdateEmailQueueSent :exec
UPDATE "EmailQueue"
SET "Status" = 'sent', "UpdatedAt" = NOW()
WHERE "Id" = $1;

-- name: UpdateEmailQueueFailed :exec
UPDATE "EmailQueue"
SET "Status" = 'fail', "Retry" = $2, "NextRetryAt" = $3, "UpdatedAt" = NOW()
WHERE "Id" = $1;
