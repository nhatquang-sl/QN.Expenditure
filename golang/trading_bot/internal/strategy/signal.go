package strategy

// Side represents the direction of a trade signal.
type Side string

const (
	SideBuy  Side = "BUY"
	SideSell Side = "SELL"
)

// Reason constants provide human-readable, machine-parseable explanations
// for individual scoring contributions included in a Signal.
const (
	ReasonRSIOversold        = "RSI_OVERSOLD"
	ReasonRSIExtremeOversold = "RSI_EXTREME_OVERSOLD"
	ReasonPriceBelowBBLower  = "PRICE_BELOW_BB_LOWER"
	ReasonRSISlopeRising     = "RSI_SLOPE_RISING"
	ReasonRSISlopeFalling    = "RSI_SLOPE_FALLING"
	ReasonBBWidthSqueeze     = "BB_WIDTH_SQUEEZE"

	ReasonRSIBullishDivergence = "RSI_BULLISH_DIVERGENCE"
	ReasonRSIBearishDivergence = "RSI_BEARISH_DIVERGENCE"
)

// Signal is the output of a strategy evaluation when gate conditions are met.
// Score quantifies signal strength; Reasons lists every scoring contribution.
type Signal struct {
	Side    Side
	Score   int
	Reasons []string
}
