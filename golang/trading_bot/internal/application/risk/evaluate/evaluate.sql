-- name: GetCandidateTrade :one
SELECT id, symbol, timeframe, side, entry_price,
       strategy_name, strategy_version,
       rsi, bb_upper, bb_middle, bb_lower, bb_width, bb_percent_b, rsi_slope,
       divergence_type, score, reasons, idempotency_key, created_at
FROM candidate_trades
WHERE id = $1;

-- name: GetAccountBalances :many
SELECT id, asset, total, available, locked, updated_at
FROM account_balances
ORDER BY asset;

-- name: GetOpenPositions :many
SELECT id, candidate_id, symbol, asset, quantity, avg_entry_price, current_price, unrealized_pnl, created_at, updated_at
FROM positions
WHERE quantity > 0
ORDER BY symbol;

-- name: InsertPortfolioSnapshot :exec
INSERT INTO portfolio_snapshots (id, candidate_id, equity, available_cash, total_exposure, unrealized_pnl, open_positions, captured_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
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

-- name: InsertRiskContext :exec
INSERT INTO risk_contexts (id, candidate_id, portfolio_snapshot_id, generated_at, raw_json)
VALUES ($1, $2, $3, $4, $5)
ON CONFLICT (candidate_id) DO NOTHING;

-- name: InsertRiskDecision :exec
INSERT INTO risk_decisions (id, candidate_id, risk_context_id, decision, position_size, reasons, created_at)
VALUES ($1, $2, $3, $4, $5, $6, $7)
ON CONFLICT (candidate_id) DO NOTHING;
