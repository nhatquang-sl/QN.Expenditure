package indicator

// DivergenceConfig holds parameters for RSI divergence detection.
// These live in IndicatorConfig (not strategy config) because detection is an
// indicator-layer computation stamped into Snapshot.DivergenceType.
type DivergenceConfig struct {
	PeakThreshold   float64 // RSI must be strictly above this to be a peak candidate (default 68)
	TroughThreshold float64 // RSI must be strictly below this to be a trough candidate (default 32)
	LookbackCandles int     // how many RSI values back to scan for a prior peak/trough (default 20)
}

// Config holds the parameters for all indicator calculations.
type Config struct {
	RSIPeriod      int
	BBPeriod       int
	BBMultiplier   float64
	RSISlopePeriod int
	Divergence     DivergenceConfig
}

// DefaultConfig returns the standard indicator configuration.
func DefaultConfig() Config {
	return Config{
		RSIPeriod:      14,
		BBPeriod:       20,
		BBMultiplier:   2.0,
		RSISlopePeriod: 1,
		Divergence: DivergenceConfig{
			PeakThreshold:   68.0,
			TroughThreshold: 32.0,
			LookbackCandles: 20,
		},
	}
}
