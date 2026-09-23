package trade

import (
	"time"

	"trading_bot/internal/indicator"
	"trading_bot/internal/strategy"
)

// CandidateTrade captures the full decision snapshot for a single strategy signal.
// It is the stable contract boundary consumed by downstream components (Risk Engine, LLM).
type CandidateTrade struct {
	Id              string
	Symbol          string
	Timeframe       string
	Side            strategy.Side
	EntryPrice      float64
	StrategyName    string
	StrategyVersion string
	Indicators      indicator.Snapshot
	Score           int
	Reasons         []string
	IdempotencyKey  string
	CreatedAt       time.Time
}
