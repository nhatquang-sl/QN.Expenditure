package divergence

import (
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
	"trading_bot/internal/indicator"
	"trading_bot/internal/strategy"
)

func ctx(divType indicator.DivergenceType, rsiSlope float64) strategy.MarketContext {
	return strategy.MarketContext{
		Indicators: indicator.Snapshot{
			DivergenceType: divType,
			RSISlope:       rsiSlope,
		},
	}
}

var s = New()

func TestDivergenceStrategy_NoDivergence_ReturnsNil(t *testing.T) {
	sig, err := s.Evaluate(ctx(indicator.DivergenceNone, 0.5))
	require.NoError(t, err)
	assert.Nil(t, sig)
}

func TestDivergenceStrategy_Bullish_SlopeConfirming_Score65(t *testing.T) {
	sig, err := s.Evaluate(ctx(indicator.DivergenceBullish, 0.5))
	require.NoError(t, err)
	require.NotNil(t, sig)
	assert.Equal(t, strategy.SideBuy, sig.Side)
	assert.Equal(t, 65, sig.Score)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSIBullishDivergence)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeRising)
}

func TestDivergenceStrategy_Bullish_SlopeCounter_Score35(t *testing.T) {
	sig, err := s.Evaluate(ctx(indicator.DivergenceBullish, 0.0))
	require.NoError(t, err)
	require.NotNil(t, sig)
	assert.Equal(t, strategy.SideBuy, sig.Side)
	assert.Equal(t, 35, sig.Score)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSIBullishDivergence)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeFalling)
}

func TestDivergenceStrategy_Bearish_SlopeConfirming_Score65(t *testing.T) {
	sig, err := s.Evaluate(ctx(indicator.DivergenceBearish, -0.5))
	require.NoError(t, err)
	require.NotNil(t, sig)
	assert.Equal(t, strategy.SideSell, sig.Side)
	assert.Equal(t, 65, sig.Score)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSIBearishDivergence)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeFalling)
}

func TestDivergenceStrategy_Bearish_SlopeCounter_Score35(t *testing.T) {
	sig, err := s.Evaluate(ctx(indicator.DivergenceBearish, 0.0))
	require.NoError(t, err)
	require.NotNil(t, sig)
	assert.Equal(t, strategy.SideSell, sig.Side)
	assert.Equal(t, 35, sig.Score)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSIBearishDivergence)
	assert.Contains(t, sig.Reasons, strategy.ReasonRSISlopeRising)
}

func TestDivergenceStrategy_Determinism(t *testing.T) {
	input := ctx(indicator.DivergenceBullish, 0.5)
	sig1, err1 := s.Evaluate(input)
	sig2, err2 := s.Evaluate(input)
	require.NoError(t, err1)
	require.NoError(t, err2)
	assert.Equal(t, sig1.Side, sig2.Side)
	assert.Equal(t, sig1.Score, sig2.Score)
	assert.Equal(t, sig1.Reasons, sig2.Reasons)
}
