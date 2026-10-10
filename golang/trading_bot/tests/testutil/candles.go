package testutil

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"time"

	"trading_bot/internal/marketdata"
)

// fixtureCandle mirrors marketdata.Candle with JSON tags for fixture files.
type fixtureCandle struct {
	Symbol    string    `json:"symbol"`
	Timeframe string    `json:"timeframe"`
	OpenTime  time.Time `json:"open_time"`
	CloseTime time.Time `json:"close_time"`
	Open      float64   `json:"open"`
	High      float64   `json:"high"`
	Low       float64   `json:"low"`
	Close     float64   `json:"close"`
	Volume    float64   `json:"volume"`
	IsClosed  bool      `json:"is_closed"`
}

// FileCandleRepository loads candles from a JSON fixture file.
// Intended for tests; not for production use.
type FileCandleRepository struct {
	path string
}

func NewFileCandleRepository(path string) *FileCandleRepository {
	return &FileCandleRepository{path: path}
}

func (r *FileCandleRepository) GetClosedCandles(ctx context.Context, symbol, timeframe string, limit int) ([]marketdata.Candle, error) {
	data, err := os.ReadFile(r.path)
	if err != nil {
		return nil, fmt.Errorf("reading fixture file %q: %w", r.path, err)
	}

	var raw []fixtureCandle
	if err := json.Unmarshal(data, &raw); err != nil {
		return nil, fmt.Errorf("parsing fixture file %q: %w", r.path, err)
	}

	var candles []marketdata.Candle
	for _, fc := range raw {
		if fc.Symbol == symbol && fc.Timeframe == timeframe && fc.IsClosed {
			candles = append(candles, marketdata.Candle{
				Symbol:    fc.Symbol,
				Timeframe: fc.Timeframe,
				OpenTime:  fc.OpenTime,
				CloseTime: fc.CloseTime,
				Open:      fc.Open,
				High:      fc.High,
				Low:       fc.Low,
				Close:     fc.Close,
				Volume:    fc.Volume,
				IsClosed:  fc.IsClosed,
			})
		}
	}

	if len(candles) > limit {
		candles = candles[len(candles)-limit:]
	}

	return candles, nil
}
