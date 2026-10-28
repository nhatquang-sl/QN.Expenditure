-- name: InsertCandidateTrade :execresult
INSERT INTO candidate_trades (
    id, symbol, timeframe, side, entry_price,
    strategy_name, strategy_version,
    rsi, bb_upper, bb_middle, bb_lower, bb_width, bb_percent_b, rsi_slope,
    divergence_type, score, reasons, idempotency_key, created_at
) VALUES (
    $1, $2, $3, $4, $5, $6, $7,
    $8, $9, $10, $11, $12, $13, $14,
    $15, $16, $17, $18, $19
) ON CONFLICT (idempotency_key) DO NOTHING;
