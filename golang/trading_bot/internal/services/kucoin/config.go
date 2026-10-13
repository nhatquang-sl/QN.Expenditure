package kucoin

// Config holds KuCoin API credentials. Empty credentials are fine for public
// (unauthenticated) endpoints such as GetClosedCandles.
type Config struct {
	APIKey     string
	APISecret  string
	Passphrase string
}
