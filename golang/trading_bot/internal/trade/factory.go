package trade

import (
	"crypto/rand"
	"fmt"
	"sync"
	"time"

	"trading_bot/internal/indicator"
	"trading_bot/internal/marketdata"
	"trading_bot/internal/strategy"
)

// Factory creates CandidateTrade records from a Signal + Candle + IndicatorSnapshot.
// An in-memory set prevents duplicate creation within a single process run.
// For cross-restart deduplication, use Repository.Save which enforces the DB unique constraint.
type Factory struct {
	mu   sync.Mutex
	seen map[string]struct{}
}

func NewFactory() *Factory {
	return &Factory{seen: make(map[string]struct{})}
}

// Create returns a new CandidateTrade, or nil if a trade with the same idempotency key
// has already been created in this process run.
// Idempotency key format: Symbol:Timeframe:CloseTime:StrategyName:StrategyVersion
func (f *Factory) Create(
	candle marketdata.Candle,
	snap indicator.Snapshot,
	sig *strategy.Signal,
	strategyName, strategyVersion string,
) *CandidateTrade {
	key := fmt.Sprintf("%s:%s:%s:%s:%s",
		candle.Symbol,
		candle.Timeframe,
		candle.CloseTime.UTC().Format(time.RFC3339),
		strategyName,
		strategyVersion,
	)

	f.mu.Lock()
	defer f.mu.Unlock()

	if _, exists := f.seen[key]; exists {
		return nil
	}
	f.seen[key] = struct{}{}

	return &CandidateTrade{
		Id:              newUUID(),
		Symbol:          candle.Symbol,
		Timeframe:       candle.Timeframe,
		Side:            sig.Side,
		EntryPrice:      candle.Close,
		StrategyName:    strategyName,
		StrategyVersion: strategyVersion,
		Indicators:      snap,
		Score:           sig.Score,
		Reasons:         sig.Reasons,
		IdempotencyKey:  key,
		CreatedAt:       time.Now().UTC(),
	}
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40 // version 4
	b[8] = (b[8] & 0x3f) | 0x80 // variant bits
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
