package marketdata

import "time"

// Candle represents a single OHLCV candlestick, independent of any exchange format.
type Candle struct {
	Symbol    string
	Timeframe string

	OpenTime  time.Time
	CloseTime time.Time

	Open   float64
	High   float64
	Low    float64
	Close  float64
	Volume float64

	IsClosed bool
}
