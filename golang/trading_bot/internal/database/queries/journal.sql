-- name: InsertRiskContext :exec
INSERT INTO risk_contexts (id, candidate_id, portfolio_snapshot_id, generated_at, raw_json)
VALUES ($1, $2, $3, $4, $5)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: InsertRiskDecision :exec
INSERT INTO risk_decisions (id, candidate_id, risk_context_id, decision, position_size, reasons, created_at)
VALUES ($1, $2, $3, $4, $5, $6, $7)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: InsertPaperOrder :exec
INSERT INTO paper_orders (id, candidate_id, risk_decision_id, symbol, side, quantity, fill_price, status, created_at, updated_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: UpdatePaperOrderStatus :exec
UPDATE paper_orders
SET status     = $2,
    updated_at = $3
WHERE id = $1;

-- name: InsertPaperFill :exec
INSERT INTO paper_fills (id, candidate_id, paper_order_id, quantity, price, executed_at)
VALUES ($1, $2, $3, $4, $5, $6)
ON CONFLICT (paper_order_id) DO NOTHING;

-- name: InsertTradeOutcome :exec
INSERT INTO trade_outcomes (id, candidate_id, paper_fill_id, entry_price, exit_price, quantity, gross_pnl, net_pnl, closed_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: GetDailyPnL :one
SELECT COALESCE(SUM(net_pnl), 0)::DOUBLE PRECISION AS daily_pnl
FROM trade_outcomes
WHERE closed_at >= $1;

-- name: GetRecentTradeOutcomes :many
SELECT id, candidate_id, paper_fill_id, entry_price, exit_price, quantity, gross_pnl, net_pnl, closed_at
FROM trade_outcomes
ORDER BY closed_at DESC
LIMIT $1;
