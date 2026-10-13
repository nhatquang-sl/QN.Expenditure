package kucoin

// Balance is a single asset balance returned by KuCoin's accounts endpoint.
type Balance struct {
	Asset     string
	Total     float64
	Available float64
	Locked    float64
}
