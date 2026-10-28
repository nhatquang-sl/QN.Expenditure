-- name: GetFilledPaperOrder :one
SELECT id, candidate_id, risk_decision_id, symbol, side, quantity, fill_price, status, created_at, updated_at
FROM paper_orders
WHERE id = $1 AND status = 'FILLED';

-- name: GetPaperFillByOrderId :one
SELECT id, candidate_id, paper_order_id, quantity, price, executed_at
FROM paper_fills
WHERE paper_order_id = $1;

-- name: InsertTradeOutcome :exec
INSERT INTO trade_outcomes (id, candidate_id, paper_fill_id, entry_price, exit_price, quantity, gross_pnl, net_pnl, closed_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: ClosePosition :exec
UPDATE positions
SET quantity   = 0,
    updated_at = $2
WHERE symbol = $1;
