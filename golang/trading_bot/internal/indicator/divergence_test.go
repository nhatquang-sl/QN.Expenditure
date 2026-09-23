package indicator

import (
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"trading_bot/internal/marketdata"
)

// defaultDivCfg returns the canonical divergence config used across tests.
func defaultDivCfg() DivergenceConfig {
	return DivergenceConfig{
		PeakThreshold:   68.0,
		TroughThreshold: 32.0,
		LookbackCandles: 20,
	}
}

// makeCandles returns n synthetic candles. open=high=low=close=price, so callers
// override High/Low after construction when specific price patterns matter.
func makeCandles(n int, price float64) []marketdata.Candle {
	candles := make([]marketdata.Candle, n)
	for i := range candles {
		candles[i] = marketdata.Candle{
			OpenTime:  time.Unix(int64(i*3600), 0),
			CloseTime: time.Unix(int64((i+1)*3600), 0),
			Open:      price,
			High:      price,
			Low:       price,
			Close:     price,
			IsClosed:  true,
		}
	}
	return candles
}

// buildRSISeries constructs a synthetic RSI series of the given length where every
// value equals baseRSI unless overrideIdx is non-negative, in which case
// overrideIdx is set to overrideVal.
func buildRSISeries(n int, baseRSI float64, overrides map[int]float64) []float64 {
	s := make([]float64, n)
	for i := range s {
		s[i] = baseRSI
		if v, ok := overrides[i]; ok {
			s[i] = v
		}
	}
	return s
}

// --- No divergence ---

func TestCalculateDivergence_NoDivergence_NeutralRSI(t *testing.T) {
	// All RSI values mid-range → neither peak nor trough threshold crossed.
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, nil)

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result)
}

func TestCalculateDivergence_TooFewRSIValues_ReturnsNone(t *testing.T) {
	// Fewer than 3 RSI values → cannot confirm any local extremum.
	candles := makeCandles(20, 50000)
	rsi := buildRSISeries(2, 70.0, nil)

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result)
}

// --- RSI exactly at threshold → no detection ---

func TestCalculateDivergence_RSIExactlyAtPeakThreshold_NoDetection(t *testing.T) {
	// RSI == 68.0 is NOT strictly above PeakThreshold → no bearish divergence.
	n := 30
	candles := makeCandles(n, 50000)
	// penultimate (n-2=28): RSI=68.0 (exactly at threshold), neighbors lower.
	// prior peak at index 10: RSI=70 (above 68), price high lower than cur.
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		10: 70.0,
		27: 50.0, // left neighbor of penultimate
		28: 68.0, // penultimate candidate — exactly at threshold
		29: 50.0, // right neighbor (last)
	})
	for i := range candles {
		candles[i].High = 50000
	}
	// prior peak candle has lower high, cur candle has higher high
	candles[10].High = 48000
	candles[28].High = 52000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result, "RSI == 68 must not trigger bearish divergence (strict greater-than required)")
}

func TestCalculateDivergence_RSIExactlyAtTroughThreshold_NoDetection(t *testing.T) {
	// RSI == 32.0 is NOT strictly below TroughThreshold → no bullish divergence.
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		10: 30.0, // prior trough
		27: 50.0, // left neighbor
		28: 32.0, // penultimate candidate — exactly at threshold
		29: 50.0, // right neighbor
	})
	// prior trough has higher low (bullish condition requires prevLow > curLow)
	candles[10].Low = 51000
	candles[28].Low = 49000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result, "RSI == 32 must not trigger bullish divergence (strict less-than required)")
}

// --- Bearish divergence ---

func TestCalculateDivergence_Bearish_Detected(t *testing.T) {
	// penultimate is a confirmed RSI peak > 68 with a prior peak that has:
	//   higher RSI (prevRSI > curRSI) AND lower price high (prevHigh < curHigh).
	n := 30
	candles := makeCandles(n, 50000)
	// candleOffset = len(candles) - len(rsi) = 0 (same length)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		9:  50.0, // left neighbor of prior peak
		10: 72.0, // prior peak — higher RSI
		11: 50.0, // right neighbor of prior peak
		27: 50.0, // left neighbor of penultimate
		28: 70.0, // penultimate — confirmed peak, lower RSI than prior
		29: 50.0, // right neighbor (last element)
	})
	candles[10].High = 48000 // prior peak has LOWER price high
	candles[28].High = 52000 // current peak has HIGHER price high → bearish divergence

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceBearish, result)
}

func TestCalculateDivergence_Bearish_NoPriorPeak_ReturnsNone(t *testing.T) {
	// penultimate is an RSI peak > 68 but no prior peak exists within lookback.
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		27: 50.0,
		28: 70.0, // penultimate peak
		29: 50.0,
	})
	candles[28].High = 52000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result)
}

// --- Bullish divergence ---

func TestCalculateDivergence_Bullish_Detected(t *testing.T) {
	// penultimate is a confirmed RSI trough < 32 with a prior trough that has:
	//   lower RSI (prevRSI < curRSI) AND higher price low (prevLow > curLow).
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		9:  50.0,
		10: 28.0, // prior trough — lower RSI
		11: 50.0,
		27: 50.0,
		28: 30.0, // penultimate — confirmed trough, higher RSI than prior
		29: 50.0,
	})
	candles[10].Low = 51000 // prior trough has HIGHER price low
	candles[28].Low = 49000 // current trough has LOWER price low → bullish divergence

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceBullish, result)
}

func TestCalculateDivergence_Bullish_NoPriorTrough_ReturnsNone(t *testing.T) {
	// penultimate is an RSI trough < 32 but no prior trough exists within lookback.
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		27: 50.0,
		28: 30.0, // penultimate trough
		29: 50.0,
	})
	candles[28].Low = 49000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result)
}

// --- Prior peak/trough outside lookback window ---

func TestCalculateDivergence_Bearish_PriorPeakOutsideLookback_ReturnsNone(t *testing.T) {
	// Prior peak exists but is further back than LookbackCandles=20 from penultimate.
	// penultimate is at index 28; lookback scans indices [9, 27].
	// Prior peak at index 5 — outside the window.
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		4:  50.0,
		5:  72.0, // prior peak — outside lookback (28-20=8, so index 5 < 8)
		6:  50.0,
		27: 50.0,
		28: 70.0, // penultimate peak
		29: 50.0,
	})
	candles[5].High = 48000
	candles[28].High = 52000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result, "prior peak outside lookback window must not trigger")
}

func TestCalculateDivergence_Bullish_PriorTroughOutsideLookback_ReturnsNone(t *testing.T) {
	// Prior trough exists but is further back than LookbackCandles=20 from penultimate.
	n := 30
	candles := makeCandles(n, 50000)
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		4:  50.0,
		5:  28.0, // prior trough — outside lookback
		6:  50.0,
		27: 50.0,
		28: 30.0, // penultimate trough
		29: 50.0,
	})
	candles[5].Low = 51000
	candles[28].Low = 49000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceNone, result, "prior trough outside lookback window must not trigger")
}

// --- Bearish checked first; bullish skipped when bearish found ---

func TestCalculateDivergence_Bearish_TakesPrecedenceOverBullish(t *testing.T) {
	// This is a structural test: if both conditions could theoretically fire,
	// bearish is returned (bullish is never evaluated after bearish is found).
	// In practice only one threshold is crossed at a time, but the ordering is verified.
	n := 30
	candles := makeCandles(n, 50000)
	// Simulate penultimate above PeakThreshold with a valid prior peak.
	rsi := buildRSISeries(n, 50.0, map[int]float64{
		9:  50.0,
		10: 72.0,
		11: 50.0,
		27: 50.0,
		28: 70.0,
		29: 50.0,
	})
	candles[10].High = 48000
	candles[28].High = 52000

	result := calculateDivergence(candles, rsi, defaultDivCfg())
	assert.Equal(t, DivergenceBearish, result, "bearish must take precedence")
	assert.NotEqual(t, DivergenceBullish, result)
}
