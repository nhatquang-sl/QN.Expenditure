package indicator

// Config holds the parameters for all indicator calculations.
type Config struct {
	RSIPeriod      int
	BBPeriod       int
	BBMultiplier   float64
	RSISlopePeriod int
}

// DefaultConfig returns the standard indicator configuration.
func DefaultConfig() Config {
	return Config{
		RSIPeriod:      14,
		BBPeriod:       20,
		BBMultiplier:   2.0,
		RSISlopePeriod: 1,
	}
}
