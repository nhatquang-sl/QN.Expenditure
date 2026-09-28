-- name: DeleteStaleSessionHistories :execrows
DELETE FROM "UserSessionHistories"
WHERE "Id" IN (
    SELECT "Id" FROM "UserSessionHistories"
    WHERE "CreatedAt" < NOW() - INTERVAL '7 days'
    ORDER BY "Id"
    LIMIT 10000
);

-- name: DeleteStaleSessions :execrows
DELETE FROM "UserSessions"
WHERE "Id" IN (
    SELECT "Id" FROM "UserSessions"
    WHERE "CreatedAt" < NOW() - INTERVAL '7 days'
    ORDER BY "Id"
    LIMIT 10000
);
