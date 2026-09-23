package indicator

import "trading_bot/internal/marketdata"

// DivergenceType represents the type of RSI divergence detected on the penultimate candle.
type DivergenceType int

const (
	// DivergenceNone means no divergence pattern was detected.
	DivergenceNone DivergenceType = 0
	// DivergenceBearish means price made a higher high while RSI made a lower high (SELL signal).
	DivergenceBearish DivergenceType = 1
	// DivergenceBullish means price made a lower low while RSI made a higher low (BUY signal).
	DivergenceBullish DivergenceType = 2
)

// calculateDivergence detects RSI divergence by examining the penultimate RSI candidate
// (rsiSeries[n-2]). The penultimate value is used because its right neighbor (rsiSeries[n-1])
// exists, confirming whether it forms a local extremum.
//
// candleOffset = len(candles) - len(rsiSeries), so rsiSeries[i] maps to candles[candleOffset+i].
//
// Bearish divergence: current is an RSI peak (strict > PeakThreshold, confirmed by both
// neighbors being lower) AND a previous peak within LookbackCandles has higher RSI but
// lower price high.
//
// Bullish divergence: current is an RSI trough (strict < TroughThreshold, confirmed by both
// neighbors being higher) AND a previous trough within LookbackCandles has lower RSI but
// higher price low.
//
// Bearish is evaluated first; if found, bullish check is skipped.
// Returns DivergenceNone when there are fewer than 3 RSI values (cannot confirm extremum).
func calculateDivergence(candles []marketdata.Candle, rsiSeries []float64, cfg DivergenceConfig) DivergenceType {
	n := len(rsiSeries)
	if n < 3 {
		return DivergenceNone
	}

	candleOffset := len(candles) - n
	curIdx := n - 2 // penultimate RSI index
	curRSI := rsiSeries[curIdx]
	curCandle := candles[candleOffset+curIdx]

	// --- Bearish divergence check ---
	if curRSI > cfg.PeakThreshold {
		// Confirm local peak: both neighbors must be strictly lower.
		if rsiSeries[curIdx-1] < curRSI && rsiSeries[curIdx+1] < curRSI {
			start := curIdx - cfg.LookbackCandles
			if start < 1 {
				start = 1
			}
			for i := curIdx - 1; i >= start; i-- {
				prevRSI := rsiSeries[i]
				if prevRSI > cfg.PeakThreshold &&
					rsiSeries[i-1] < prevRSI && rsiSeries[i+1] < prevRSI {
					// Prior confirmed peak: bearish if prevRSI > curRSI AND prevHigh < curHigh.
					if prevRSI > curRSI && candles[candleOffset+i].High < curCandle.High {
						return DivergenceBearish
					}
				}
			}
		}
	}

	// --- Bullish divergence check ---
	if curRSI < cfg.TroughThreshold {
		// Confirm local trough: both neighbors must be strictly higher.
		if rsiSeries[curIdx-1] > curRSI && rsiSeries[curIdx+1] > curRSI {
			start := curIdx - cfg.LookbackCandles
			if start < 1 {
				start = 1
			}
			for i := curIdx - 1; i >= start; i-- {
				prevRSI := rsiSeries[i]
				if prevRSI < cfg.TroughThreshold &&
					rsiSeries[i-1] > prevRSI && rsiSeries[i+1] > prevRSI {
					// Prior confirmed trough: bullish if prevRSI < curRSI AND prevLow > curLow.
					if prevRSI < curRSI && candles[candleOffset+i].Low > curCandle.Low {
						return DivergenceBullish
					}
				}
			}
		}
	}

	return DivergenceNone
}
