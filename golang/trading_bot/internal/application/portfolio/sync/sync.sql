-- name: DeleteAllAccountBalances :exec
DELETE FROM account_balances;

-- name: UpsertAccountBalance :exec
INSERT INTO account_balances (id, asset, total, available, locked, updated_at)
VALUES ($1, $2, $3, $4, $5, $6)
ON CONFLICT (asset) DO UPDATE
    SET total      = EXCLUDED.total,
        available  = EXCLUDED.available,
        locked     = EXCLUDED.locked,
        updated_at = EXCLUDED.updated_at;
