-- name: GetApprovedDecisionsWithoutOrder :many
SELECT rd.id, rd.candidate_id, rd.risk_context_id, rd.position_size,
       ct.symbol, ct.side, ct.entry_price
FROM risk_decisions rd
JOIN candidate_trades ct ON ct.id = rd.candidate_id
LEFT JOIN paper_orders po ON po.candidate_id = rd.candidate_id
WHERE rd.decision = 'APPROVE'
  AND po.id IS NULL;

-- name: InsertPaperOrder :exec
INSERT INTO paper_orders (id, candidate_id, risk_decision_id, symbol, side, quantity, fill_price, status, created_at, updated_at)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
ON CONFLICT (candidate_id) DO NOTHING;
