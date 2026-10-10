package findcandidatetrade

import (
	"context"
	"log/slog"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"

	"trading_bot/internal/database/generated"
	"trading_bot/internal/indicator"
	"trading_bot/internal/services/kucoin"
	"trading_bot/internal/strategy"
	"trading_bot/internal/strategy/divergence"
	"trading_bot/internal/trade"

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
	factory   *trade.Factory
	queries   *generated.Queries
	logger    *slog.Logger
}

// NewHandler returns a Handler[Command, Result] with all dependencies injected.
func NewHandler(
	kucoinSvc *kucoin.Service,
	eng indicator.Engine,
	strat strategy.Strategy,
	factory *trade.Factory,
	queries *generated.Queries,
	logger *slog.Logger,
) app.Handler[Command, Result] {
	return &handler{
		kucoinSvc: kucoinSvc,
		eng:       eng,
		strat:     strat,
		factory:   factory,
		queries:   queries,
		logger:    logger,
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

	ct := h.factory.Create(lastCandle, snap, sig, divergence.StrategyName, divergence.StrategyVersion)
	if ct == nil {
		return Result{}, nil // in-memory duplicate within this process run
	}

	result, err := h.queries.InsertCandidateTrade(ctx, generated.InsertCandidateTradeParams{
		Id:              ct.Id,
		Symbol:          ct.Symbol,
		Timeframe:       ct.Timeframe,
		Side:            string(ct.Side),
		EntryPrice:      ct.EntryPrice,
		StrategyName:    ct.StrategyName,
		StrategyVersion: ct.StrategyVersion,
		Rsi:             ct.Indicators.RSI,
		BbUpper:         ct.Indicators.BBUpper,
		BbMiddle:        ct.Indicators.BBMiddle,
		BbLower:         ct.Indicators.BBLower,
		BbWidth:         ct.Indicators.BBWidth,
		BbPercentB:      ct.Indicators.BBPercentB,
		RsiSlope:        ct.Indicators.RSISlope,
		DivergenceType:  int32(ct.Indicators.DivergenceType),
		Score:           int32(ct.Score),
		Reasons:         ct.Reasons,
		IdempotencyKey:  ct.IdempotencyKey,
		CreatedAt:       ct.CreatedAt,
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
			slog.String("id", ct.Id),
			slog.String("symbol", ct.Symbol),
			slog.String("timeframe", ct.Timeframe),
			slog.String("side", string(ct.Side)),
			slog.String("strategy_name", ct.StrategyName),
			slog.String("strategy_version", ct.StrategyVersion),
			slog.String("idempotency_key", ct.IdempotencyKey),
			slog.Int("score", ct.Score),
		)
	}

	return Result{TradeId: ct.Id}, nil
}
