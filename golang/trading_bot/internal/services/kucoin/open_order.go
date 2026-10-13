package kucoin

// OpenOrder is an active unfilled or partially-filled order on KuCoin.
type OpenOrder struct {
	Symbol     string
	Side       string
	Size       float64
	Price      float64
	FilledSize float64
}
