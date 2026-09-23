package rsibb

import "trading_bot/internal/strategy"

// RSIBBStrategy implements the RSI+BB mean-reversion BUY signal strategy (v1.0.0).
// It satisfies the strategy.Strategy interface.
type RSIBBStrategy struct {
	cfg Config
}

// New returns an RSIBBStrategy using the provided configuration.
func New(cfg Config) *RSIBBStrategy {
	return &RSIBBStrategy{cfg: cfg}
}

// Evaluate returns a BUY Signal when RSI < RSIOversoldThreshold AND Close < BBLower.
// Returns nil when either gate condition is not met (strict less-than; equality does not trigger).
// Scoring breakdown (BUY side):
//   - RSI < RSIOversoldThreshold:        +30 (RSI_OVERSOLD)
//   - RSI < RSIExtremeOversoldThreshold: +45 instead of +30 (RSI_EXTREME_OVERSOLD; replaces, not additive)
//   - Close < BBLower:                   +30 (PRICE_BELOW_BB_LOWER)
//   - RSI Slope > 0:                     +15 (RSI_SLOPE_RISING)
//   - RSI Slope <= 0:                    -15 (RSI_SLOPE_FALLING)
//   - BBWidth < BBWidthThreshold:        +10 (BB_WIDTH_SQUEEZE)
func (s *RSIBBStrategy) Evaluate(ctx strategy.MarketContext) (*strategy.Signal, error) {
	ind := ctx.Indicators
	close := ctx.Candle.Close

	// Gate: both conditions must be satisfied simultaneously (strict less-than).
	if ind.RSI >= s.cfg.RSIOversoldThreshold || close >= ind.BBLower {
		return nil, nil
	}

	sig := &strategy.Signal{Side: strategy.SideBuy}

	// RSI scoring: extreme tier replaces the standard tier (not additive).
	if ind.RSI < s.cfg.RSIExtremeOversoldThreshold {
		sig.Score += 45
		sig.Reasons = append(sig.Reasons, strategy.ReasonRSIExtremeOversold)
	} else {
		sig.Score += 30
		sig.Reasons = append(sig.Reasons, strategy.ReasonRSIOversold)
	}

	// Price below lower Bollinger Band.
	sig.Score += 30
	sig.Reasons = append(sig.Reasons, strategy.ReasonPriceBelowBBLower)

	// RSI slope direction: rising adds confidence, falling reduces it.
	if ind.RSISlope > 0 {
		sig.Score += 15
		sig.Reasons = append(sig.Reasons, strategy.ReasonRSISlopeRising)
	} else {
		sig.Score -= 15
		sig.Reasons = append(sig.Reasons, strategy.ReasonRSISlopeFalling)
	}

	// BB width squeeze bonus (scoring only, not a gate condition).
	if ind.BBWidth < s.cfg.BBWidthThreshold {
		sig.Score += 10
		sig.Reasons = append(sig.Reasons, strategy.ReasonBBWidthSqueeze)
	}

	return sig, nil
}
