package indicator

// Snapshot holds all computed indicator values for a single closed candle.
// Consumed by the strategy engine; never modified after creation.
type Snapshot struct {
	RSI float64

	BBUpper    float64
	BBMiddle   float64
	BBLower    float64
	BBWidth    float64
	BBPercentB float64

	RSISlope float64
}
