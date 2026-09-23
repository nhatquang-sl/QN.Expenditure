package rsibb_test

import (
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"trading_bot/internal/indicator"
	"trading_bot/internal/marketdata"
	"trading_bot/internal/strategy"
	"trading_bot/internal/strategy/rsibb"
)

// makeCtx builds a MarketContext with the given close price and indicator values.
// Fields not listed default to zero.
func makeCtx(close, rsi, bbLower, bbWidth, rsiSlope float64) strategy.MarketContext {
	return strategy.MarketContext{
		Candle: marketdata.Candle{Close: close},
		Indicators: indicator.Snapshot{
			RSI:      rsi,
			BBLower:  bbLower,
			BBWidth:  bbWidth,
			RSISlope: rsiSlope,
		},
	}
}

// --- Gate conditions ---

func TestEvaluate_NoSignal_NeitherCondition(t *testing.T) {
	// RSI >= 30 AND Close >= BBLower → no signal
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(44000, 55, 43000, 0.03, 1))
	require.NoError(t, err)
	assert.Nil(t, sig, "expected no signal when neither gate condition is met")
}

func TestEvaluate_NoSignal_RSIOversoldOnly(t *testing.T) {
	// RSI < 30 but Close >= BBLower → no signal (both conditions required)
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(44000, 28, 43000, 0.03, 1))
	require.NoError(t, err)
	assert.Nil(t, sig, "expected no signal when only RSI is oversold")
}

func TestEvaluate_NoSignal_PriceBelowBBLowerOnly(t *testing.T) {
	// Close < BBLower but RSI >= 30 → no signal (both conditions required)
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 35, 43000, 0.03, 1))
	require.NoError(t, err)
	assert.Nil(t, sig, "expected no signal when only price is below BBLower")
}

func TestEvaluate_BuySignal_BothConditionsMet(t *testing.T) {
	// RSI < 30 AND Close < BBLower → BUY signal
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 28, 43000, 0.03, 1))
	require.NoError(t, err)
	require.NotNil(t, sig, "expected a BUY signal when both gate conditions are met")
	assert.Equal(t, strategy.SideBuy, sig.Side)
	assert.Greater(t, sig.Score, 0)
	assert.Contains(t, sig.Reasons, strategy.ReasonPriceBelowBBLower)
}

// --- Boundary conditions ---

func TestEvaluate_Boundary_RSI_ExactlyThreshold_NoSignal(t *testing.T) {
	// RSI == 30 (not strictly less than) → no signal
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 30.0, 43000, 0.03, 1))
	require.NoError(t, err)
	assert.Nil(t, sig, "RSI == 30 must not trigger (strict less-than required)")
}

func TestEvaluate_Boundary_RSI_JustBelowThreshold_Signal(t *testing.T) {
	// RSI == 29.9999 (just below 30) → BUY signal
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 29.9999, 43000, 0.03, 1))
	require.NoError(t, err)
	require.NotNil(t, sig, "RSI == 29.9999 must trigger a BUY signal")
	assert.Equal(t, strategy.SideBuy, sig.Side)
}

func TestEvaluate_Boundary_Close_ExactlyBBLower_NoSignal(t *testing.T) {
	// Close == BBLower (not strictly less than) → no signal
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(43000, 28, 43000, 0.03, 1))
	require.NoError(t, err)
	assert.Nil(t, sig, "Close == BBLower must not trigger (strict less-than required)")
}

func TestEvaluate_Boundary_Close_JustBelowBBLower_Signal(t *testing.T) {
	// Close == BBLower - 0.01 (just below) → BUY signal
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42999.99, 28, 43000, 0.03, 1))
	require.NoError(t, err)
	require.NotNil(t, sig, "Close just below BBLower must trigger a BUY signal")
	assert.Equal(t, strategy.SideBuy, sig.Side)
}

// --- Scoring ---

func TestEvaluate_Scoring_RSIOversold_Standard(t *testing.T) {
	// RSI in [25, 30) → +30 for RSI_OVERSOLD, not extreme tier
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 27, 43000, 0.03, -1)) // slope <= 0 → -15
	require.NoError(t, err)
	require.NotNil(t, sig)

	assert.Contains(t, sig.Reasons, strategy.ReasonRSIOversold)
	assert.NotContains(t, sig.Reasons, strategy.ReasonRSIExtremeOversold)
	// Score: +30 (RSI) + 30 (BB) - 15 (slope) = 45
	assert.Equal(t, 45, sig.Score)
}

func TestEvaluate_Scoring_RSIExtremeOversold_Replaces_NotAdditive(t *testing.T) {
	// RSI < 25 → +45, not +75 (extreme tier replaces standard, not stacked)
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 24, 43000, 0.03, -1)) // slope <= 0 → -15
	require.NoError(t, err)
	require.NotNil(t, sig)

	assert.Contains(t, sig.Reasons, strategy.ReasonRSIExtremeOversold)
	assert.NotContains(t, sig.Reasons, strategy.ReasonRSIOversold)
	// Score: +45 (RSI extreme) + 30 (BB) - 15 (slope) = 60
	assert.Equal(t, 60, sig.Score)
}

func TestEvaluate_Scoring_RSISlope_Rising_AddsConfidence(t *testing.T) {
	// RSI Slope > 0 → +15
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 27, 43000, 0.03, 0.5))
	require.NoError(t, err)
	require.NotNil(t, sig)

	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeRising)
	assert.NotContains(t, sig.Reasons, strategy.ReasonRSISlopeFalling)
	// Score: +30 (RSI) + 30 (BB) + 15 (slope) = 75
	assert.Equal(t, 75, sig.Score)
}

func TestEvaluate_Scoring_RSISlope_Falling_ReducesConfidence(t *testing.T) {
	// RSI Slope <= 0 → -15
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 27, 43000, 0.03, -0.5))
	require.NoError(t, err)
	require.NotNil(t, sig)

	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeFalling)
	assert.NotContains(t, sig.Reasons, strategy.ReasonRSISlopeRising)
	// Score: +30 (RSI) + 30 (BB) - 15 (slope) = 45
	assert.Equal(t, 45, sig.Score)
}

func TestEvaluate_Scoring_BBWidthSqueeze_AddsTen(t *testing.T) {
	// BBWidth < 0.02 → +10 squeeze bonus
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 27, 43000, 0.01, 0.5)) // narrow bands
	require.NoError(t, err)
	require.NotNil(t, sig)

	assert.Contains(t, sig.Reasons, strategy.ReasonBBWidthSqueeze)
	// Score: +30 (RSI) + 30 (BB) + 15 (slope) + 10 (squeeze) = 85
	assert.Equal(t, 85, sig.Score)
}

func TestEvaluate_Scoring_BBWidthAboveThreshold_NoBonus(t *testing.T) {
	// BBWidth >= 0.02 → no squeeze bonus (scoring-only threshold, not a gate)
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 27, 43000, 0.02, 0.5))
	require.NoError(t, err)
	require.NotNil(t, sig)

	assert.NotContains(t, sig.Reasons, strategy.ReasonBBWidthSqueeze)
	// Score: +30 (RSI) + 30 (BB) + 15 (slope) = 75
	assert.Equal(t, 75, sig.Score)
}

func TestEvaluate_Scoring_MaxScore(t *testing.T) {
	// RSI < 25, Close < BBLower, slope > 0, BBWidth < 0.02 → maximum BUY score
	strat := rsibb.New(rsibb.DefaultConfig())
	sig, err := strat.Evaluate(makeCtx(42000, 20, 43000, 0.01, 1.0))
	require.NoError(t, err)
	require.NotNil(t, sig)

	// Score: +45 (RSI extreme) + 30 (BB) + 15 (slope) + 10 (squeeze) = 100
	assert.Equal(t, 100, sig.Score)
	assert.Equal(t, strategy.SideBuy, sig.Side)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSIExtremeOversold)
	assert.Contains(t, sig.Reasons, strategy.ReasonPriceBelowBBLower)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeRising)
	assert.Contains(t, sig.Reasons, strategy.ReasonBBWidthSqueeze)
}

func TestEvaluate_Deterministic(t *testing.T) {
	// Same inputs must produce identical output (no hidden mutable state).
	strat := rsibb.New(rsibb.DefaultConfig())
	ctx := makeCtx(42000, 24, 43000, 0.01, 1.0)

	sig1, err := strat.Evaluate(ctx)
	require.NoError(t, err)
	sig2, err := strat.Evaluate(ctx)
	require.NoError(t, err)

	require.NotNil(t, sig1)
	require.NotNil(t, sig2)
	assert.Equal(t, sig1.Side, sig2.Side)
	assert.Equal(t, sig1.Score, sig2.Score)
	assert.Equal(t, sig1.Reasons, sig2.Reasons)
}
