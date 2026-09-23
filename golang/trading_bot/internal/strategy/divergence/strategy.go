package divergence

import (
	"trading_bot/internal/indicator"
	"trading_bot/internal/strategy"
)

// DivergenceStrategy implements the RSI Divergence signal strategy (v1.0.0).
// It satisfies the strategy.Strategy interface.
type DivergenceStrategy struct{}

// New returns a DivergenceStrategy.
func New() *DivergenceStrategy {
	return &DivergenceStrategy{}
}

// Evaluate returns a Signal when a bullish or bearish RSI divergence is detected in
// the snapshot. Returns nil when DivergenceType is None.
// Scoring breakdown:
//   - Divergence detected (base):           +50
//   - RSI Slope confirming direction:       +15 (BUY: slope > 0; SELL: slope < 0)
//   - RSI Slope counter to direction:       -15
//   - Score range: 35–65
func (s *DivergenceStrategy) Evaluate(ctx strategy.MarketContext) (*strategy.Signal, error) {
	ind := ctx.Indicators

	switch ind.DivergenceType {
	case indicator.DivergenceBullish:
		sig := &strategy.Signal{
			Side:    strategy.SideBuy,
			Score:   50,
			Reasons: []string{strategy.ReasonRSIBullishDivergence},
		}
		if ind.RSISlope > 0 {
			sig.Score += 15
			sig.Reasons = append(sig.Reasons, strategy.ReasonRSISlopeRising)
		} else {
			sig.Score -= 15
			sig.Reasons = append(sig.Reasons, strategy.ReasonRSISlopeFalling)
		}
		return sig, nil

	case indicator.DivergenceBearish:
		sig := &strategy.Signal{
			Side:    strategy.SideSell,
			Score:   50,
			Reasons: []string{strategy.ReasonRSIBearishDivergence},
		}
		if ind.RSISlope < 0 {
			sig.Score += 15
			sig.Reasons = append(sig.Reasons, strategy.ReasonRSISlopeFalling)
		} else {
			sig.Score -= 15
			sig.Reasons = append(sig.Reasons, strategy.ReasonRSISlopeRising)
		}
		return sig, nil

	default:
		return nil, nil
	}
}
