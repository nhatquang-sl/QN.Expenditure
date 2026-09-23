package rsibb

const (
	// StrategyName and StrategyVersion are stamped on every CandidateTrade.
	// Bump StrategyVersion whenever strategy logic or scoring changes.
	StrategyName    = "RSI_BB_MEAN_REVERSION"
	StrategyVersion = "1.0.0"
)

// Config holds the RSI+BB mean-reversion strategy parameters.
// All fields are part of the versioned strategy contract: changing any value
// requires a StrategyVersion bump so backtests remain attributable.
type Config struct {
	// Gate thresholds (strict less-than applied)
	RSIOversoldThreshold float64 // RSI < this triggers BUY gate (default 30)

	// Scoring thresholds
	RSIExtremeOversoldThreshold float64 // RSI < this scores +45 instead of +30 (default 25)
	BBWidthThreshold            float64 // BBWidth < this adds squeeze bonus +10 (default 0.02)
}

// DefaultConfig returns the canonical v1.0.0 strategy configuration.
func DefaultConfig() Config {
	return Config{
		RSIOversoldThreshold:        30.0,
		RSIExtremeOversoldThreshold: 25.0,
		BBWidthThreshold:            0.02,
	}
}
