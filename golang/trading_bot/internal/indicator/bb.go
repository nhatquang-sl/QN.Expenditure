package indicator

import (
	"fmt"
	"math"
)

type bbResult struct {
	upper    float64
	middle   float64
	lower    float64
	width    float64
	percentB float64
}

// calculateBB computes Bollinger Bands(period, multiplier) for the last element of closes.
// Uses population standard deviation (/ N), matching TradingView's BB calculation.
// BBPercentB returns 0.5 when Upper == Lower (constant-price edge case).
// Returns an error if len(closes) < period.
func calculateBB(closes []float64, period int, multiplier float64) (bbResult, error) {
	if len(closes) < period {
		return bbResult{}, fmt.Errorf("BB requires at least %d closes, got %d", period, len(closes))
	}

	window := closes[len(closes)-period:]

	var sum float64
	for _, v := range window {
		sum += v
	}
	middle := sum / float64(period)

	var variance float64
	for _, v := range window {
		dev := v - middle
		variance += dev * dev
	}
	variance /= float64(period)
	stddev := math.Sqrt(variance)

	upper := middle + multiplier*stddev
	lower := middle - multiplier*stddev

	var width float64
	if middle != 0 {
		width = (upper - lower) / middle
	}

	percentB := 0.5
	if upper != lower {
		percentB = (closes[len(closes)-1] - lower) / (upper - lower)
	}

	return bbResult{
		upper:    upper,
		middle:   middle,
		lower:    lower,
		width:    width,
		percentB: percentB,
	}, nil
}
