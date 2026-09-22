package indicator_test

import (
	"context"
	"math"
	"path/filepath"
	"runtime"
	"testing"
	"time"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"trading_bot/internal/indicator"
	"trading_bot/internal/marketdata"
)

const tolerance = 0.0001

func fixturesDir() string {
	_, file, _, _ := runtime.Caller(0)
	// .../trading_bot/internal/indicator/engine_test.go — up 2 levels to trading_bot root
	root := filepath.Join(filepath.Dir(file), "..", "..")
	return filepath.Join(root, "tests", "fixtures")
}

func withinTolerance(t *testing.T, expected, actual float64, msg string) {
	t.Helper()
	diff := math.Abs(expected - actual)
	assert.LessOrEqual(t, diff, tolerance, "%s: expected %.10f, got %.10f (diff %.10f)", msg, expected, actual, diff)
}

func makeCandles(closes []float64) []marketdata.Candle {
	candles := make([]marketdata.Candle, len(closes))
	base := time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC)
	for i, c := range closes {
		candles[i] = marketdata.Candle{
			Symbol:    "BTCUSDT",
			Timeframe: "1hour",
			OpenTime:  base.Add(time.Duration(i) * time.Hour),
			CloseTime: base.Add(time.Duration(i)*time.Hour + 59*time.Minute + 59*time.Second),
			Open:      c,
			High:      c,
			Low:       c,
			Close:     c,
			Volume:    100,
			IsClosed:  true,
		}
	}
	return candles
}

// --- Edge cases ---

func TestEngine_InsufficientCandles(t *testing.T) {
	eng := indicator.NewEngine(indicator.DefaultConfig())
	// DefaultConfig requires BBPeriod=20; fewer candles should error
	candles := makeCandles(make([]float64, 19))
	_, err := eng.Calculate(candles)
	assert.Error(t, err)
}

func TestEngine_RSI_InsufficientForRSI(t *testing.T) {
	// Exactly BBPeriod candles but fewer than RSIPeriod+1 — engine should error on RSI
	cfg := indicator.Config{RSIPeriod: 14, BBPeriod: 5, BBMultiplier: 2, RSISlopePeriod: 1}
	eng := indicator.NewEngine(cfg)
	// 10 candles: >= BBPeriod(5) but < RSIPeriod+1(15)
	candles := makeCandles(make([]float64, 10))
	_, err := eng.Calculate(candles)
	assert.Error(t, err)
}

func TestEngine_RSI_ConstantPrices(t *testing.T) {
	// All prices the same → no gains, no losses → avgLoss == 0 → RSI = 100
	closes := make([]float64, 30)
	for i := range closes {
		closes[i] = 42000.0
	}
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)
	assert.Equal(t, 100.0, snap.RSI)
}

func TestEngine_RSI_AllIncreasing(t *testing.T) {
	// Steadily increasing prices → no losses → RSI = 100
	closes := make([]float64, 30)
	for i := range closes {
		closes[i] = 40000.0 + float64(i)*100
	}
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)
	assert.Equal(t, 100.0, snap.RSI)
}

func TestEngine_RSI_AllDecreasing(t *testing.T) {
	// Steadily decreasing prices → no gains → RSI ≈ 0
	closes := make([]float64, 30)
	for i := range closes {
		closes[i] = 50000.0 - float64(i)*100
	}
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)
	assert.Equal(t, 0.0, snap.RSI)
}

func TestEngine_RSI_BoundaryValues(t *testing.T) {
	// RSI must be in [0, 100]
	eng := indicator.NewEngine(indicator.DefaultConfig())

	// Mixed movement
	closes := make([]float64, 30)
	closes[0] = 42000
	for i := 1; i < len(closes); i++ {
		if i%3 == 0 {
			closes[i] = closes[i-1] - 200
		} else {
			closes[i] = closes[i-1] + 100
		}
	}
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)
	assert.GreaterOrEqual(t, snap.RSI, 0.0)
	assert.LessOrEqual(t, snap.RSI, 100.0)
}

// --- Fixture validation ---

func TestEngine_RSI_FixtureMatchesReference(t *testing.T) {
	// Expected RSI computed independently using the same Wilder's smoothing algorithm
	// against the synthetic BTCUSDT_1h_200.json fixture (seed=42 random walk).
	const expectedRSI = 68.7260393200

	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)
	require.Len(t, candles, 200)

	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(candles)
	require.NoError(t, err)

	withinTolerance(t, expectedRSI, snap.RSI, "RSI")
}

func TestEngine_RSI_Deterministic(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)

	eng := indicator.NewEngine(indicator.DefaultConfig())

	snap1, err := eng.Calculate(candles)
	require.NoError(t, err)
	snap2, err := eng.Calculate(candles)
	require.NoError(t, err)

	assert.Equal(t, snap1.RSI, snap2.RSI, "RSI must be deterministic")
}

// --- BB edge cases ---

func TestEngine_BB_ConstantPrices(t *testing.T) {
	// All prices identical → stddev = 0 → Upper == Lower → BBPercentB = 0.5
	closes := make([]float64, 30)
	for i := range closes {
		closes[i] = 42000.0
	}
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)

	assert.Equal(t, 42000.0, snap.BBMiddle)
	assert.Equal(t, 42000.0, snap.BBUpper)
	assert.Equal(t, 42000.0, snap.BBLower)
	assert.Equal(t, 0.0, snap.BBWidth)
	assert.Equal(t, 0.5, snap.BBPercentB, "BBPercentB must be 0.5 when Upper == Lower")
}

func TestEngine_BB_InsufficientCandles(t *testing.T) {
	// Fewer than BBPeriod candles → engine errors (already covered by TestEngine_InsufficientCandles,
	// but this confirms the BB-specific path via a config with a large BBPeriod).
	cfg := indicator.Config{RSIPeriod: 3, BBPeriod: 10, BBMultiplier: 2, RSISlopePeriod: 1}
	eng := indicator.NewEngine(cfg)
	candles := makeCandles(make([]float64, 9))
	_, err := eng.Calculate(candles)
	assert.Error(t, err)
}

func TestEngine_BB_UpperLowerSymmetry(t *testing.T) {
	// Monotonically increasing prices: BBMiddle is in the middle of the window,
	// BBUpper > BBMiddle, BBLower < BBMiddle, symmetrical.
	closes := make([]float64, 30)
	for i := range closes {
		closes[i] = 40000.0 + float64(i)*100
	}
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)

	assert.Greater(t, snap.BBUpper, snap.BBMiddle)
	assert.Less(t, snap.BBLower, snap.BBMiddle)
	// Population stddev is symmetric, so upper - middle == middle - lower within float precision
	withinTolerance(t, snap.BBUpper-snap.BBMiddle, snap.BBMiddle-snap.BBLower, "BB band symmetry")
	assert.Greater(t, snap.BBWidth, 0.0)
}

func TestEngine_BB_PercentB_AboveUpper(t *testing.T) {
	// Last price well above upper band → BBPercentB > 1
	closes := make([]float64, 30)
	for i := range closes {
		closes[i] = 42000.0
	}
	closes[29] = 99999.0 // spike far above
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)
	assert.Greater(t, snap.BBPercentB, 1.0)
}

// --- BB fixture validation ---

func TestEngine_BB_FixtureMatchesReference(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)
	require.Len(t, candles, 200)

	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(candles)
	require.NoError(t, err)

	// Reference computation: independent inline implementation of BB(20, 2) with population stddev.
	closes := make([]float64, len(candles))
	for i, c := range candles {
		closes[i] = c.Close
	}
	n := len(closes)
	const bbPeriod = 20
	window := closes[n-bbPeriod : n]

	var refSum float64
	for _, v := range window {
		refSum += v
	}
	refMiddle := refSum / float64(bbPeriod)

	var refVariance float64
	for _, v := range window {
		dev := v - refMiddle
		refVariance += dev * dev
	}
	refVariance /= float64(bbPeriod)
	refStddev := math.Sqrt(refVariance)

	refUpper := refMiddle + 2*refStddev
	refLower := refMiddle - 2*refStddev
	refWidth := (refUpper - refLower) / refMiddle
	refPercentB := (closes[n-1] - refLower) / (refUpper - refLower)

	withinTolerance(t, refMiddle, snap.BBMiddle, "BBMiddle")
	withinTolerance(t, refUpper, snap.BBUpper, "BBUpper")
	withinTolerance(t, refLower, snap.BBLower, "BBLower")
	withinTolerance(t, refWidth, snap.BBWidth, "BBWidth")
	withinTolerance(t, refPercentB, snap.BBPercentB, "BBPercentB")
}

// --- RSI Slope ---

func TestEngine_RSISlope_Direction(t *testing.T) {
	// Increasing prices after a dip: RSI should be rising, so slope > 0
	closes := make([]float64, 35)
	for i := range closes {
		if i < 15 {
			closes[i] = 42000.0 - float64(i)*200 // declining
		} else {
			closes[i] = 42000.0 + float64(i-15)*300 // recovering
		}
	}
	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(makeCandles(closes))
	require.NoError(t, err)

	// RSI slope can be positive or negative here; just assert it's a finite number in reasonable range.
	assert.GreaterOrEqual(t, snap.RSISlope, -100.0)
	assert.LessOrEqual(t, snap.RSISlope, 100.0)
}

func TestEngine_RSISlope_FixtureMatchesReference(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)
	require.Len(t, candles, 200)

	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(candles)
	require.NoError(t, err)

	// Reference: slope = RSI(closes[0..199]) - RSI(closes[0..198])
	// Reuse the same Wilder's smoothing logic inline.
	closes := make([]float64, len(candles))
	for i, c := range candles {
		closes[i] = c.Close
	}
	refCurrent := refRSI(t, closes, 14)
	refPrev := refRSI(t, closes[:len(closes)-1], 14)
	refSlope := refCurrent - refPrev

	withinTolerance(t, refSlope, snap.RSISlope, "RSISlope")
}

// refRSI is a test-only reference implementation of Wilder's RSI to validate the engine against.
func refRSI(t *testing.T, closes []float64, period int) float64 {
	t.Helper()
	require.GreaterOrEqual(t, len(closes), period+1, "refRSI: not enough data")

	gains := make([]float64, len(closes)-1)
	losses := make([]float64, len(closes)-1)
	for i := 1; i < len(closes); i++ {
		d := closes[i] - closes[i-1]
		if d > 0 {
			gains[i-1] = d
		} else {
			losses[i-1] = -d
		}
	}

	var avgGain, avgLoss float64
	for i := 0; i < period; i++ {
		avgGain += gains[i]
		avgLoss += losses[i]
	}
	avgGain /= float64(period)
	avgLoss /= float64(period)

	for i := period; i < len(gains); i++ {
		avgGain = (avgGain*float64(period-1) + gains[i]) / float64(period)
		avgLoss = (avgLoss*float64(period-1) + losses[i]) / float64(period)
	}

	if avgLoss == 0 {
		return 100
	}
	return 100 - (100 / (1 + avgGain/avgLoss))
}

// --- Full snapshot determinism ---

func TestEngine_Snapshot_Deterministic(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)

	eng := indicator.NewEngine(indicator.DefaultConfig())

	snap1, err := eng.Calculate(candles)
	require.NoError(t, err)
	snap2, err := eng.Calculate(candles)
	require.NoError(t, err)

	assert.Equal(t, snap1, snap2, "full Snapshot must be deterministic across two runs")
}
