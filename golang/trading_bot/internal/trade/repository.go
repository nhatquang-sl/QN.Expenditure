package trade

import (
	"context"
	"database/sql"
	"log/slog"

	"trading_bot/internal/database/generated"
)

// Repository persists CandidateTrade records to PostgreSQL.
// Duplicate inserts (same idempotency_key) are silently ignored via ON CONFLICT DO NOTHING.
type Repository struct {
	queries *generated.Queries
	logger  *slog.Logger
}

func NewRepository(db *sql.DB, logger *slog.Logger) *Repository {
	return &Repository{
		queries: generated.New(db),
		logger:  logger,
	}
}

// Save inserts the trade into the database. If the idempotency key already exists,
// the insert is skipped silently. A structured log event is emitted only on a new insert.
func (r *Repository) Save(ctx context.Context, trade *CandidateTrade) error {
	result, err := r.queries.InsertCandidateTrade(ctx, generated.InsertCandidateTradeParams{
		ID:              trade.Id,
		Symbol:          trade.Symbol,
		Timeframe:       trade.Timeframe,
		Side:            string(trade.Side),
		EntryPrice:      trade.EntryPrice,
		StrategyName:    trade.StrategyName,
		StrategyVersion: trade.StrategyVersion,
		Rsi:             trade.Indicators.RSI,
		BbUpper:         trade.Indicators.BBUpper,
		BbMiddle:        trade.Indicators.BBMiddle,
		BbLower:         trade.Indicators.BBLower,
		BbWidth:         trade.Indicators.BBWidth,
		BbPercentB:      trade.Indicators.BBPercentB,
		RsiSlope:        trade.Indicators.RSISlope,
		DivergenceType:  int32(trade.Indicators.DivergenceType),
		Score:           int32(trade.Score),
		Reasons:         trade.Reasons,
		IdempotencyKey:  trade.IdempotencyKey,
		CreatedAt:       trade.CreatedAt,
	})
	if err != nil {
		return err
	}

	affected, err := result.RowsAffected()
	if err != nil {
		return err
	}

	if affected == 1 {
		r.logger.InfoContext(ctx, "candidate_trade_created",
			slog.String("id", trade.Id),
			slog.String("symbol", trade.Symbol),
			slog.String("timeframe", trade.Timeframe),
			slog.String("side", string(trade.Side)),
			slog.String("strategy_name", trade.StrategyName),
			slog.String("strategy_version", trade.StrategyVersion),
			slog.String("idempotency_key", trade.IdempotencyKey),
			slog.Int("score", trade.Score),
		)
	}

	return nil
}
