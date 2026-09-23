package strategy

import (
	"trading_bot/internal/indicator"
	"trading_bot/internal/marketdata"
)

// MarketContext is the input to a strategy evaluation.
// It carries the closed candle that triggered evaluation and the
// pre-computed indicator snapshot for that candle.
type MarketContext struct {
	Candle     marketdata.Candle
	Indicators indicator.Snapshot
}
