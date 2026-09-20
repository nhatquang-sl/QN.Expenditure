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
