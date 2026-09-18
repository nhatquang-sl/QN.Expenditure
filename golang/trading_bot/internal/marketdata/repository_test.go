package marketdata_test

import (
	"context"
	"path/filepath"
	"runtime"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"trading_bot/internal/marketdata"
)

// fixturesDir returns the absolute path to the tests/fixtures directory.
func fixturesDir() string {
	_, file, _, _ := runtime.Caller(0)
	// file is .../trading_bot/internal/marketdata/repository_test.go
	// go up 2 levels: marketdata/ → internal/ → trading_bot/
	root := filepath.Join(filepath.Dir(file), "..", "..")
	return filepath.Join(root, "tests", "fixtures")
}

func TestFileCandleRepository_LoadsBTCUSDT1h(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))

	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)

	assert.Len(t, candles, 200)
	assert.Equal(t, "BTCUSDT", candles[0].Symbol)
	assert.Equal(t, "1hour", candles[0].Timeframe)
	assert.True(t, candles[0].IsClosed)
	assert.True(t, candles[0].OpenTime.Before(candles[1].OpenTime), "candles should be oldest-first")
}

func TestFileCandleRepository_RespectsLimit(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))

	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 50)
	require.NoError(t, err)

	assert.Len(t, candles, 50)
}

func TestFileCandleRepository_FiltersBySymbolAndTimeframe(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))

	candles, err := repo.GetClosedCandles(context.Background(), "ETHUSDT", "1hour", 1000)
	require.NoError(t, err)
	assert.Empty(t, candles)

	candles, err = repo.GetClosedCandles(context.Background(), "BTCUSDT", "4hour", 1000)
	require.NoError(t, err)
	assert.Empty(t, candles)
}

func TestFileCandleRepository_MissingFile(t *testing.T) {
	repo := marketdata.NewFileCandleRepository("/nonexistent/path/candles.json")

	_, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 100)
	assert.Error(t, err)
}
