package indicator

import (
	"fmt"
	"trading_bot/internal/marketdata"
)

// Engine computes all indicator values from a slice of closed candles.
type Engine interface {
	Calculate(candles []marketdata.Candle) (Snapshot, error)
}

type engine struct {
	cfg Config
}

// NewEngine returns an Engine using the provided configuration.
func NewEngine(cfg Config) Engine {
	return &engine{cfg: cfg}
}

func (e *engine) Calculate(candles []marketdata.Candle) (Snapshot, error) {
	if len(candles) < e.cfg.BBPeriod {
		return Snapshot{}, fmt.Errorf("indicator engine requires at least %d candles, got %d", e.cfg.BBPeriod, len(candles))
	}

	closes := make([]float64, len(candles))
	for i, c := range candles {
		closes[i] = c.Close
	}

	rsi, err := calculateRSI(closes, e.cfg.RSIPeriod)
	if err != nil {
		return Snapshot{}, err
	}

	return Snapshot{
		RSI: rsi,
		// BB fields populated in Slice 3
	}, nil
}
