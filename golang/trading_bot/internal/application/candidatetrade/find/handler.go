package findcandidatetrade

import (
	"context"
	"crypto/rand"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"

	"trading_bot/internal/database/generated"
	"trading_bot/internal/indicator"
	"trading_bot/internal/services/kucoin"
	"trading_bot/internal/strategy"
	"trading_bot/internal/strategy/divergence"

	app "qn.expenditure/shared/app"
)

const tracerName = "trading-bot"

// Command carries the per-call inputs for processing a symbol/timeframe pair.
type Command struct {
	Symbol      string
	Timeframe   string
	CandleLimit int
}

// Result is returned after processing. TradeId is empty when no new trade was created.
type Result struct {
	TradeId string
}

type handler struct {
	kucoinSvc *kucoin.Service
	eng       indicator.Engine
	strat     strategy.Strategy
	queries   *generated.Queries
	logger    *slog.Logger

	mu   sync.Mutex
	seen map[string]struct{}
}

// NewHandler returns a Handler[Command, Result] with all dependencies injected.
func NewHandler(
	kucoinSvc *kucoin.Service,
	eng indicator.Engine,
	strat strategy.Strategy,
	queries *generated.Queries,
	logger *slog.Logger,
) app.Handler[Command, Result] {
	return &handler{
		kucoinSvc: kucoinSvc,
		eng:       eng,
		strat:     strat,
		queries:   queries,
		logger:    logger,
		seen:      make(map[string]struct{}),
	}
}

func (h *handler) Handle(ctx context.Context, cmd Command) (Result, error) {
	ctx, span := otel.Tracer(tracerName).Start(ctx, "findCandidateTrade")
	defer span.End()
	span.SetAttributes(
		attribute.String("symbol", cmd.Symbol),
		attribute.String("timeframe", cmd.Timeframe),
		attribute.Int("candle_limit", cmd.CandleLimit),
	)

	candles, err := h.kucoinSvc.GetClosedCandles(ctx, cmd.Symbol, cmd.Timeframe, cmd.CandleLimit)
	if err != nil {
		return Result{}, err
	}
	if len(candles) == 0 {
		return Result{}, nil
	}

	snap, err := h.eng.Calculate(candles)
	if err != nil {
		return Result{}, err
	}
	span.SetAttributes(attribute.Float64("rsi", snap.RSI))

	lastCandle := candles[len(candles)-1]
	sig, err := h.strat.Evaluate(strategy.MarketContext{Candle: lastCandle, Indicators: snap})
	if err != nil {
		return Result{}, err
	}
	if sig == nil {
		return Result{}, nil
	}

	// Idempotency key: Symbol:Timeframe:CloseTime:StrategyName:StrategyVersion
	key := fmt.Sprintf("%s:%s:%s:%s:%s",
		lastCandle.Symbol,
		lastCandle.Timeframe,
		lastCandle.CloseTime.UTC().Format(time.RFC3339),
		divergence.StrategyName,
		divergence.StrategyVersion,
	)

	h.mu.Lock()
	_, duplicate := h.seen[key]
	if !duplicate {
		h.seen[key] = struct{}{}
	}
	h.mu.Unlock()

	if duplicate {
		return Result{}, nil // in-memory duplicate within this process run
	}

	id := newUUID()
	now := time.Now().UTC()

	result, err := h.queries.InsertCandidateTrade(ctx, generated.InsertCandidateTradeParams{
		Id:              id,
		Symbol:          lastCandle.Symbol,
		Timeframe:       lastCandle.Timeframe,
		Side:            string(sig.Side),
		EntryPrice:      lastCandle.Close,
		StrategyName:    divergence.StrategyName,
		StrategyVersion: divergence.StrategyVersion,
		Rsi:             snap.RSI,
		BbUpper:         snap.BBUpper,
		BbMiddle:        snap.BBMiddle,
		BbLower:         snap.BBLower,
		BbWidth:         snap.BBWidth,
		BbPercentB:      snap.BBPercentB,
		RsiSlope:        snap.RSISlope,
		DivergenceType:  int32(snap.DivergenceType),
		Score:           int32(sig.Score),
		Reasons:         sig.Reasons,
		IdempotencyKey:  key,
		CreatedAt:       now,
	})
	if err != nil {
		return Result{}, err
	}

	affected, err := result.RowsAffected()
	if err != nil {
		return Result{}, err
	}

	if affected == 1 {
		h.logger.InfoContext(ctx, "candidate_trade_created",
			slog.String("id", id),
			slog.String("symbol", lastCandle.Symbol),
			slog.String("timeframe", lastCandle.Timeframe),
			slog.String("side", string(sig.Side)),
			slog.String("strategy_name", divergence.StrategyName),
			slog.String("strategy_version", divergence.StrategyVersion),
			slog.String("idempotency_key", key),
			slog.Int("score", sig.Score),
		)
	}

	return Result{TradeId: id}, nil
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40 // version 4
	b[8] = (b[8] & 0x3f) | 0x80 // variant bits
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
