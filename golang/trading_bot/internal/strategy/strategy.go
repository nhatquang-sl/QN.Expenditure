package strategy

// Strategy evaluates a MarketContext and returns a Signal if gate conditions
// are met, or nil if no signal is generated. Implementations must be stateless
// and deterministic: identical inputs must always produce identical outputs.
type Strategy interface {
	Evaluate(ctx MarketContext) (*Signal, error)
}
