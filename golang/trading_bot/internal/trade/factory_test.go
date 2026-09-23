package trade_test

import (
	"context"
	"path/filepath"
	"runtime"
	"strings"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"trading_bot/internal/indicator"
	"trading_bot/internal/marketdata"
	"trading_bot/internal/strategy"
	"trading_bot/internal/strategy/divergence"
	"trading_bot/internal/trade"
)

func fixturesDir() string {
	_, file, _, _ := runtime.Caller(0)
	root := filepath.Join(filepath.Dir(file), "..", "..")
	return filepath.Join(root, "tests", "fixtures")
}

func runPipeline(t *testing.T) (*strategy.Signal, *trade.CandidateTrade, indicator.Snapshot) {
	t.Helper()

	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)
	require.NotEmpty(t, candles)

	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(candles)
	require.NoError(t, err)

	strat := divergence.New()
	lastCandle := candles[len(candles)-1]
	sig, err := strat.Evaluate(strategy.MarketContext{Candle: lastCandle, Indicators: snap})
	require.NoError(t, err)

	factory := trade.NewFactory()
	var ct *trade.CandidateTrade
	if sig != nil {
		ct = factory.Create(lastCandle, snap, sig, divergence.StrategyName, divergence.StrategyVersion)
	}

	return sig, ct, snap
}

// TestSeam3_DivergenceStrategy_Pipeline verifies the full wiring:
// FileCandleRepository → IndicatorEngine → DivergenceStrategy → Factory → CandidateTrade
func TestSeam3_DivergenceStrategy_Pipeline(t *testing.T) {
	sig, ct, snap := runPipeline(t)

	if sig == nil {
		// Fixture candles produce no divergence — correct per spec (no forced divergence).
		assert.Nil(t, ct, "no CandidateTrade should be created when signal is nil")
		return
	}

	require.NotNil(t, ct)
	assert.Equal(t, "BTCUSDT", ct.Symbol)
	assert.Equal(t, "1hour", ct.Timeframe)
	assert.Equal(t, divergence.StrategyName, ct.StrategyName)
	assert.Equal(t, divergence.StrategyVersion, ct.StrategyVersion)
	assert.Equal(t, sig.Side, ct.Side)
	assert.Equal(t, sig.Score, ct.Score)
	assert.Equal(t, sig.Reasons, ct.Reasons)
	assert.InDelta(t, snap.RSI, ct.Indicators.RSI, 0.0001)
	assert.NotEmpty(t, ct.Id)
	assert.NotEmpty(t, ct.IdempotencyKey)
	assert.True(t, strings.Contains(ct.IdempotencyKey, divergence.StrategyName))
	assert.True(t, strings.Contains(ct.IdempotencyKey, divergence.StrategyVersion))
	assert.False(t, ct.CreatedAt.IsZero())
}

// TestSeam3_InMemoryIdempotency verifies that calling Create twice with the same candle
// returns a trade only on the first call and nil on the second.
func TestSeam3_InMemoryIdempotency(t *testing.T) {
	repo := marketdata.NewFileCandleRepository(filepath.Join(fixturesDir(), "BTCUSDT_1h_200.json"))
	candles, err := repo.GetClosedCandles(context.Background(), "BTCUSDT", "1hour", 1000)
	require.NoError(t, err)

	eng := indicator.NewEngine(indicator.DefaultConfig())
	snap, err := eng.Calculate(candles)
	require.NoError(t, err)

	lastCandle := candles[len(candles)-1]
	fakeSig := &strategy.Signal{Side: strategy.SideBuy, Score: 65, Reasons: []string{"RSI_BULLISH_DIVERGENCE"}}

	factory := trade.NewFactory()
	first := factory.Create(lastCandle, snap, fakeSig, divergence.StrategyName, divergence.StrategyVersion)
	second := factory.Create(lastCandle, snap, fakeSig, divergence.StrategyName, divergence.StrategyVersion)

	require.NotNil(t, first, "first Create must return a trade")
	assert.Nil(t, second, "second Create with same inputs must return nil (in-memory deduplication)")
}

// TestSeam3_Determinism verifies that running the same fixture twice produces identical output.
func TestSeam3_Determinism(t *testing.T) {
	sig1, ct1, snap1 := runPipeline(t)
	sig2, ct2, snap2 := runPipeline(t)

	// Divergence type and signal side/score/reasons must be identical.
	assert.Equal(t, snap1.DivergenceType, snap2.DivergenceType)

	if sig1 == nil {
		assert.Nil(t, sig2)
		assert.Nil(t, ct1)
		assert.Nil(t, ct2)
		return
	}

	require.NotNil(t, sig2)
	assert.Equal(t, sig1.Side, sig2.Side)
	assert.Equal(t, sig1.Score, sig2.Score)
	assert.Equal(t, sig1.Reasons, sig2.Reasons)

	require.NotNil(t, ct1)
	require.NotNil(t, ct2)
	// Exclude Id (UUID) and CreatedAt (timestamp) — non-deterministic by design.
	assert.Equal(t, ct1.Symbol, ct2.Symbol)
	assert.Equal(t, ct1.Timeframe, ct2.Timeframe)
	assert.Equal(t, ct1.Side, ct2.Side)
	assert.Equal(t, ct1.EntryPrice, ct2.EntryPrice)
	assert.Equal(t, ct1.StrategyName, ct2.StrategyName)
	assert.Equal(t, ct1.StrategyVersion, ct2.StrategyVersion)
	assert.Equal(t, ct1.Score, ct2.Score)
	assert.Equal(t, ct1.Reasons, ct2.Reasons)
	assert.Equal(t, ct1.Indicators, ct2.Indicators)
	assert.Equal(t, ct1.IdempotencyKey, ct2.IdempotencyKey)
}
