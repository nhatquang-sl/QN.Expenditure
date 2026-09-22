package indicator

import "fmt"

// calculateRSI computes RSI using Wilder's Smoothing (RMA).
//
// Algorithm:
//  1. Compute price changes between consecutive closes.
//  2. Separate into gains (positive deltas) and losses (absolute negative deltas).
//  3. Seed avgGain and avgLoss with the simple average of the first `period` values.
//  4. Apply Wilder's smoothing for all remaining values:
//     avg = (prev × (period-1) + current) / period
//  5. RSI = 100 - (100 / (1 + avgGain/avgLoss)).
//     When avgLoss == 0, RSI = 100.
//
// Returns the RSI of the last candle in closes.
// Requires len(closes) >= period+1; returns an error otherwise.
func calculateRSI(closes []float64, period int) (float64, error) {
	if len(closes) < period+1 {
		return 0, fmt.Errorf("RSI requires at least %d closes, got %d", period+1, len(closes))
	}

	gains := make([]float64, len(closes)-1)
	losses := make([]float64, len(closes)-1)
	for i := 1; i < len(closes); i++ {
		delta := closes[i] - closes[i-1]
		if delta > 0 {
			gains[i-1] = delta
		} else {
			losses[i-1] = -delta
		}
	}

	// Seed: SMA of first period gains/losses
	var avgGain, avgLoss float64
	for i := 0; i < period; i++ {
		avgGain += gains[i]
		avgLoss += losses[i]
	}
	avgGain /= float64(period)
	avgLoss /= float64(period)

	// Wilder's smoothing for the remaining values
	for i := period; i < len(gains); i++ {
		avgGain = (avgGain*float64(period-1) + gains[i]) / float64(period)
		avgLoss = (avgLoss*float64(period-1) + losses[i]) / float64(period)
	}

	if avgLoss == 0 {
		return 100, nil
	}
	rs := avgGain / avgLoss
	return 100 - (100 / (1 + rs)), nil
}

// calculateRSISlope returns RSI[n] - RSI[n-slopePeriod], measuring the direction
// of RSI over the last slopePeriod candles.
// Requires len(closes) >= rsiPeriod + 1 + slopePeriod.
func calculateRSISlope(closes []float64, rsiPeriod, slopePeriod int) (float64, error) {
	if len(closes) < rsiPeriod+1+slopePeriod {
		return 0, fmt.Errorf("RSI slope requires at least %d closes, got %d", rsiPeriod+1+slopePeriod, len(closes))
	}
	current, err := calculateRSI(closes, rsiPeriod)
	if err != nil {
		return 0, err
	}
	prev, err := calculateRSI(closes[:len(closes)-slopePeriod], rsiPeriod)
	if err != nil {
		return 0, err
	}
	return current - prev, nil
}
