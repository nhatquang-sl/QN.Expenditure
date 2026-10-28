-- name: GetOpenPaperOrders :many
SELECT id, candidate_id, risk_decision_id, symbol, side, quantity, fill_price, created_at, updated_at
FROM paper_orders
WHERE status = 'OPEN'
ORDER BY created_at ASC;

-- name: UpdatePaperOrderStatus :exec
UPDATE paper_orders
SET status     = $2,
    updated_at = $3
WHERE id = $1;

-- name: InsertPaperFill :exec
INSERT INTO paper_fills (id, candidate_id, paper_order_id, quantity, price, executed_at)
VALUES ($1, $2, $3, $4, $5, $6)
ON CONFLICT (paper_order_id) DO NOTHING;

-- name: UpsertPosition :exec
INSERT INTO positions (id, candidate_id, symbol, asset, quantity, avg_entry_price, current_price, unrealized_pnl, created_at, updated_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
ON CONFLICT (symbol) DO UPDATE
    SET quantity        = EXCLUDED.quantity,
        avg_entry_price = EXCLUDED.avg_entry_price,
        current_price   = EXCLUDED.current_price,
        unrealized_pnl  = EXCLUDED.unrealized_pnl,
        updated_at      = EXCLUDED.updated_at;
