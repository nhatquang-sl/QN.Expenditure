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

-- name: GetAccountBalances :many
SELECT id, asset, total, available, locked, updated_at
FROM account_balances
ORDER BY asset;

-- name: GetAccountBalance :one
SELECT id, asset, total, available, locked, updated_at
FROM account_balances
WHERE asset = $1;

-- name: InsertPortfolioSnapshot :exec
INSERT INTO portfolio_snapshots (id, candidate_id, equity, available_cash, total_exposure, unrealized_pnl, open_positions, captured_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: GetOpenPositions :many
SELECT id, candidate_id, symbol, asset, quantity, avg_entry_price, current_price, unrealized_pnl, created_at, updated_at
FROM positions
WHERE quantity > 0
ORDER BY symbol;

-- name: UpsertPosition :exec
INSERT INTO positions (id, candidate_id, symbol, asset, quantity, avg_entry_price, current_price, unrealized_pnl, created_at, updated_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
ON CONFLICT (symbol) DO UPDATE
    SET quantity        = EXCLUDED.quantity,
        avg_entry_price = EXCLUDED.avg_entry_price,
        current_price   = EXCLUDED.current_price,
        unrealized_pnl  = EXCLUDED.unrealized_pnl,
        updated_at      = EXCLUDED.updated_at;
