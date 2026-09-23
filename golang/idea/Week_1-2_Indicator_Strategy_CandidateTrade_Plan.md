# Week 1–2 Implementation Plan
## Indicator Engine, Strategy Engine, and Stable Candidate Trade Generation

### Objective

During Week 1–2, the goal is to build a deterministic and testable trading-signal pipeline:

```text
Market Data
    ↓
Candle
    ↓
Indicator Engine
    ↓
Indicator Snapshot
    ↓
Strategy Engine
    ↓
Candidate Trade
```

At the end of Week 2, the system should be able to consume closed market candles and consistently produce the same `CandidateTrade` for the same input data.

### Scope

#### In Scope

- Candle/domain model
- Historical candle loading
- Indicator calculation
- RSI
- Bollinger Bands
- Bollinger Band Width
- Bollinger %B
- RSI slope
- Feature snapshot
- Strategy interface
- RSI + Bollinger Bands strategy
- RSI Divergence indicator (peak/trough detection)
- RSI Divergence strategy (BUY on bullish divergence, SELL on bearish divergence)
- Candidate Trade contract
- Strategy versioning
- Deterministic signal generation
- Duplicate-signal prevention
- Unit tests
- Integration tests
- Indicator validation against a reference implementation

#### Out of Scope

Do not implement these during Week 1–2:

- LLM / AI Risk Manager
- RAG
- ML prediction models
- Live order execution
- Real-money trading
- Advanced market-regime classification
- Portfolio optimization
- Dynamic strategy selection

The purpose is to establish a reliable deterministic baseline before adding AI.

---

# Week 1 — Indicator Engine

## 1. Define the Candle Domain Model

Create a stable candle representation that can be used by both live trading and backtesting.

```go
type Candle struct {
    Symbol    string
    Timeframe string

    OpenTime  time.Time
    CloseTime time.Time

    Open      decimal.Decimal
    High      decimal.Decimal
    Low       decimal.Decimal
    Close     decimal.Decimal
    Volume    decimal.Decimal

    IsClosed bool
}
```

### Requirements

- Support multiple symbols.
- Support multiple timeframes.
- Store open and close timestamps.
- Use closed candles for strategy evaluation.
- Avoid using the currently forming candle.
- Keep the model independent of any exchange-specific response format.

---

## 2. Define Indicator Snapshot

Create a single immutable-style object containing all indicators required by the strategy.

```go
type IndicatorSnapshot struct {
    RSI float64

    BBUpper  float64
    BBMiddle float64
    BBLower  float64

    BBWidth    float64
    BBPercentB float64

    RSISlope float64

    // DivergenceType is computed from the full RSI series and candle price history.
    // None = no divergence detected; Bearish = SELL candidate; Bullish = BUY candidate.
    DivergenceType DivergenceType
}
```

`DivergenceType` enum:

```go
type DivergenceType int

const (
    DivergenceNone    DivergenceType = iota // no divergence detected
    DivergenceBearish                       // price higher high + RSI lower high → SELL
    DivergenceBullish                       // price lower low  + RSI higher low  → BUY
)
```

The strategy should consume the snapshot rather than recalculating indicators itself.

---

# 3. Implement Indicator Engine

Define a clear interface:

```go
type IndicatorEngine interface {
    Calculate(candles []Candle) IndicatorSnapshot
}
```

A better production-oriented version may return an error:

```go
type IndicatorEngine interface {
    Calculate(candles []Candle) (IndicatorSnapshot, error)
}
```

### Design Principles

The Indicator Engine:

- Calculates mathematical indicators only.
- Does not know BUY or SELL.
- Does not contain trading rules.
- Does not call an exchange.
- Does not call an LLM.
- Must be deterministic.
- Must be independently testable.

---

# 4. Implement RSI

Initial configuration:

```text
RSI period = 14
```

Calculate:

```text
RSI(14)
```

### Required tests

Test:

- Insufficient candle count.
- Constant prices.
- Increasing prices.
- Decreasing prices.
- Mixed price movement.
- Known reference dataset.
- Boundary values near 0 and 100.

### Acceptance Criteria

For the same candle sequence:

```text
Calculate(candles)
```

must always produce the same RSI.

---

# 5. Implement Bollinger Bands

Initial configuration:

```text
Period = 20
Standard deviation multiplier = 2
```

Calculate:

```text
Middle = SMA(20)

Upper = Middle + 2 × StandardDeviation(20)

Lower = Middle - 2 × StandardDeviation(20)
```

### Required tests

Test:

- Insufficient candle count.
- Constant prices.
- Trending prices.
- Volatile prices.
- Known reference dataset.
- Upper/lower band relationship.

Expected invariant:

```text
Lower <= Middle <= Upper
```

---

# 6. Implement Bollinger Band Width

Calculate:

```text
BBWidth = (Upper - Lower) / Middle
```

This will later be useful for identifying volatility expansion and contraction.

### Tests

- Normal market.
- Constant/near-zero middle price handling.
- High volatility.
- Low volatility.

---

# 7. Implement Bollinger %B

Calculate:

```text
%B = (Close - Lower) / (Upper - Lower)
```

Interpretation:

```text
%B = 0      → price at lower band
%B = 0.5    → price at middle band
%B = 1      → price at upper band
%B < 0      → price below lower band
%B > 1      → price above upper band
```

This is useful for making the strategy conditions more explicit than simply checking whether price is below the lower band.

---

# 8. Implement RSI Slope

Calculate a simple initial slope:

```text
RSI Slope = Current RSI - Previous RSI
```

Interpretation:

```text
Positive → RSI is rising
Negative → RSI is falling
Zero     → RSI unchanged
```

This should remain a feature rather than a trading decision inside the Indicator Engine.

---

# 9. Create Indicator Configuration

Avoid hard-coding parameters throughout the code.

Example:

```go
type IndicatorConfig struct {
    RSIPeriod int

    BBPeriod     int
    BBMultiplier float64

    RSISlopePeriod int

    Divergence DivergenceConfig
}

type DivergenceConfig struct {
    // RSI local maximum must exceed this to qualify as a peak (strict >)
    PeakThreshold float64

    // RSI local minimum must be below this to qualify as a trough (strict <)
    TroughThreshold float64

    // How many candles back to search for a prior peak/trough
    LookbackCandles int
}
```

Default configuration (matches TradingView defaults):

```text
RSI Period        = 14
BB Period         = 20
BB Multiplier     = 2
RSI Slope Period  = 1
PeakThreshold     = 68
TroughThreshold   = 32
LookbackCandles   = 20
```

### Configuration Reference

All default values are chosen to match TradingView's built-in indicator defaults so that output can be validated against TradingView charts.

| Parameter | Default | TradingView equivalent | Meaning |
|---|---|---|---|
| `RSIPeriod` | `14` | RSI → Length = 14 | Number of candles used to compute each RSI value. More periods = smoother RSI, slower to react. |
| `BBPeriod` | `20` | Bollinger Bands → Length = 20 | SMA window for the middle band. Upper/lower bands are calculated over the same window. |
| `BBMultiplier` | `2` | Bollinger Bands → StdDev = 2 | Number of standard deviations added/subtracted from the middle band to form the upper and lower bands. At `2`, roughly 95% of price action falls inside the bands. A wider multiplier (e.g. `3`) produces fewer but more extreme signals; a narrower multiplier (e.g. `1`) produces more frequent, noisier signals. |
| `RSISlopePeriod` | `1` | *(no direct equivalent)* | How many candles back to compare RSI for slope direction. `1` means `slope = RSI[now] - RSI[prev]`. Positive = rising momentum; negative = falling momentum. |
| `PeakThreshold` | `68` | *(custom divergence logic)* | RSI local maximum must strictly exceed this value to qualify as a divergence peak. Chosen near the overbought boundary (TradingView RSI overbought default = 70) to catch meaningful peaks without false positives. |
| `TroughThreshold` | `32` | *(custom divergence logic)* | RSI local minimum must strictly be below this value to qualify as a divergence trough. Chosen near the oversold boundary (TradingView RSI oversold default = 30) to catch meaningful troughs without false positives. |
| `LookbackCandles` | `20` | *(custom divergence logic)* | Maximum number of candles to look back when searching for a prior peak or trough. A window of 20 candles covers roughly one BB period, keeping divergence signals locally relevant. |

This makes future backtesting and optimization easier.

---

# 10. Implement RSI Divergence Detection

The Indicator Engine computes `DivergenceType` from the full RSI series and candle price history. This is the Go port of the C# `CalculateRsiPeaksAndTroughsCommand` + `DivergenceRsiPeakCommand` / `DivergenceRsiTroughCommand` logic.

### Step 1 — Build the RSI series

`calculateRSI()` currently returns a single scalar. Refactor (or add a companion `calculateRSISeries()`) to return a full `[]float64` series — one value per candle starting from index `RSIPeriod`. The last element is the same value as the existing `RSI` field (no change to external output).

### Step 2 — Find peaks and troughs

Walk the RSI series from index 1 to `len-2` (skip first and last):

```text
Peak   : rsi[i] > rsi[i-1]  AND  rsi[i] > rsi[i+1]  AND  rsi[i] > PeakThreshold   (strict >)
Trough : rsi[i] < rsi[i-1]  AND  rsi[i] < rsi[i+1]  AND  rsi[i] < TroughThreshold  (strict <)
```

### Step 3 — Check divergence at the last closed candle

Evaluate `candles[len-1]` (the last closed candle). Look back up to `LookbackCandles` (default 20) for a prior peak or trough.

| Divergence | Condition | Result |
|---|---|---|
| **Bearish** | Current is a peak AND ∃ previous peak where `prevRSI > curRSI` AND `prevHigh < curHigh` | `DivergenceBearish` |
| **Bullish** | Current is a trough AND ∃ previous trough where `prevRSI < curRSI` AND `prevLow > curLow` | `DivergenceBullish` |

Bearish is checked first; if found, bullish check is skipped.

Interpretation:
- Bearish: price makes a **higher high** while RSI makes a **lower high** → momentum weakening on the up-move → SELL candidate
- Bullish: price makes a **lower low** while RSI makes a **higher low** → momentum recovering on the down-move → BUY candidate

### Design Principles

- Divergence detection belongs in the Indicator Engine, not the Strategy.
- The Strategy reads `DivergenceType` from `IndicatorSnapshot` — it never looks at raw candles.
- `calculateDivergence()` is a pure function: `(candles []Candle, rsiSeries []float64, cfg DivergenceConfig) DivergenceType`.

### Required tests

- No peaks/troughs in series → `DivergenceNone`
- Bearish divergence manually constructed → `DivergenceBearish`
- Bullish divergence manually constructed → `DivergenceBullish`
- Prior peak/trough outside lookback window → `DivergenceNone`
- RSI exactly at threshold (68 or 32) → not detected (strict inequality)

---

# 11. Indicator Validation

Before moving to the Strategy Engine, validate the implementation against a trusted reference.

Possible references:

- TradingView
- TA-Lib
- A known dataset
- Independently calculated expected values

Do not compare only the final trading result.

Compare:

```text
RSI
BB Upper
BB Middle
BB Lower
BB Width
BB %B
RSI Slope
DivergenceType
```

### Acceptance Criteria

The differences must be within an agreed numerical tolerance.

For example:

```text
absolute difference <= 0.000001
```

The exact tolerance can depend on the numeric representation.

---

# Week 2 — Strategy Engine

## 11. Define Strategy Interface

The Strategy Engine should be independent of the Indicator Engine implementation.

```go
type Strategy interface {
    Evaluate(ctx MarketContext) (*Signal, error)
}
```

---

# 12. Define Market Context

The strategy should receive all information required for evaluation.

```go
type MarketContext struct {
    Symbol    string
    Timeframe string

    Candle     Candle
    Indicators IndicatorSnapshot
}
```

Later, this can be extended with features and market regime:

```go
type MarketContext struct {
    Symbol    string
    Timeframe string

    Candle     Candle
    Indicators IndicatorSnapshot
    Features   Features
    Regime     MarketRegime
}
```

Do not add unnecessary fields during the MVP.

---

# 13. Define Signal

The strategy should first generate a logical signal.

```go
type Side string

const (
    Buy  Side = "BUY"
    Sell Side = "SELL"
)

type Signal struct {
    Side    Side
    Score   float64
    Reasons []string
}
```

Reason constants (machine-parseable, human-readable):

```go
const (
    // RSI + BB mean-reversion strategy
    ReasonRSIOversold        = "RSI_OVERSOLD"
    ReasonRSIExtremeOversold = "RSI_EXTREME_OVERSOLD"
    ReasonPriceBelowBBLower  = "PRICE_BELOW_BB_LOWER"
    ReasonRSISlopeRising     = "RSI_SLOPE_RISING"
    ReasonRSISlopeFalling    = "RSI_SLOPE_FALLING"
    ReasonBBWidthSqueeze     = "BB_WIDTH_SQUEEZE"

    // RSI Divergence strategy
    ReasonRSIBullishDivergence = "RSI_BULLISH_DIVERGENCE"
    ReasonRSIBearishDivergence = "RSI_BEARISH_DIVERGENCE"
)
```

The signal should describe **why** the strategy detected an opportunity.

---

# 14. Implement RSI + Bollinger Bands Strategy

Initial strategy:

### Long Candidate

Generate a BUY signal when:

```text
RSI < 30
AND
Close < Bollinger Lower Band
```

Example:

```go
func (s *RSIBBStrategy) Evaluate(
    ctx MarketContext,
) (*Signal, error) {

    if ctx.Indicators.RSI >= 30 {
        return nil, nil
    }

    if ctx.Candle.Close >= ctx.Indicators.BBLower {
        return nil, nil
    }

    return &Signal{
        Side: Buy,
        Reasons: []string{
            "RSI_OVERSOLD",
            "PRICE_BELOW_BB_LOWER",
        },
    }, nil
}
```

For the first version, keep the strategy simple.

Do not add many conditions simply because they might improve backtest performance.

---

# 15. Implement RSI Divergence Strategy

The Divergence Strategy is the second `Strategy` implementation alongside RSI+BB. It reads `DivergenceType` directly from `IndicatorSnapshot` — no candle history is needed inside the strategy itself.

### Gate condition

```text
ctx.Indicators.DivergenceType != DivergenceNone
```

### Signal direction

```text
DivergenceBullish → Side = BUY,  Reason = RSI_BULLISH_DIVERGENCE
DivergenceBearish → Side = SELL, Reason = RSI_BEARISH_DIVERGENCE
```

### Scoring

| Condition | Score |
|---|---|
| Divergence detected (base) | +50 |
| RSI Slope confirming direction | +15 |
| RSI Slope counter to direction | -15 |

Confirming direction:
- BUY signal: RSI Slope > 0 → +15 (`RSI_SLOPE_RISING`); RSI Slope ≤ 0 → -15 (`RSI_SLOPE_FALLING`)
- SELL signal: RSI Slope < 0 → +15 (`RSI_SLOPE_FALLING`); RSI Slope ≥ 0 → -15 (`RSI_SLOPE_RISING`)

Score range: **35–65**.

### Strategy configuration

```go
const (
    DivergenceStrategyName    = "RSI_DIVERGENCE"
    DivergenceStrategyVersion = "1.0.0"
)

// Config has no tunable parameters for v1.0.0.
// All detection parameters (PeakThreshold, TroughThreshold, LookbackCandles)
// live in DivergenceConfig inside IndicatorConfig.
type DivergenceStrategyConfig struct{}
```

### Example idempotency key

```text
BTCUSDT:1hour:2026-09-16T10:00:00Z:RSI_DIVERGENCE:1.0.0
```

### Example implementation sketch

```go
func (s *DivergenceStrategy) Evaluate(ctx MarketContext) (*Signal, error) {
    div := ctx.Indicators.DivergenceType
    if div == DivergenceNone {
        return nil, nil
    }

    sig := &Signal{}
    if div == DivergenceBullish {
        sig.Side = Buy
        sig.Reasons = append(sig.Reasons, ReasonRSIBullishDivergence)
    } else {
        sig.Side = Sell
        sig.Reasons = append(sig.Reasons, ReasonRSIBearishDivergence)
    }
    sig.Score += 50

    // RSI Slope: confirming = +15, counter = -15
    slope := ctx.Indicators.RSISlope
    confirming := (sig.Side == Buy && slope > 0) || (sig.Side == Sell && slope < 0)
    if confirming {
        sig.Score += 15
        sig.Reasons = append(sig.Reasons, ReasonRSISlopeRising)
    } else {
        sig.Score -= 15
        sig.Reasons = append(sig.Reasons, ReasonRSISlopeFalling)
    }

    return sig, nil
}
```

### Required tests

Construct `MarketContext` directly — no candles, no engine:

| Case | Input | Expected |
|---|---|---|
| No signal | `DivergenceNone` | `nil` |
| BUY, slope rising | `DivergenceBullish`, slope > 0 | `Side=BUY`, score=65, `RSI_BULLISH_DIVERGENCE` |
| BUY, slope falling | `DivergenceBullish`, slope ≤ 0 | `Side=BUY`, score=35 |
| SELL, slope falling | `DivergenceBearish`, slope < 0 | `Side=SELL`, score=65, `RSI_BEARISH_DIVERGENCE` |
| SELL, slope rising | `DivergenceBearish`, slope ≥ 0 | `Side=SELL`, score=35 |
| Determinism | same `MarketContext` twice | identical `Side`, `Score`, `Reasons` |

---

# 16. Add Strategy Configuration

Avoid hard-coding strategy parameters.

```go
type RSIBBConfig struct {
    RSIOversold float64
    RSIOverbought float64

    RequirePriceBelowLowerBB bool
}
```

Example:

```text
RSI Oversold = 30
RSI Overbought = 70
Require Price Below Lower BB = true
```

This makes strategy versions and backtesting easier.

---

# 16. Define Candidate Trade

`CandidateTrade` is the key contract between the deterministic strategy layer and the future Risk/AI layer.

```go
type CandidateTrade struct {
    ID string

    Symbol    string
    Timeframe string

    Side       Side
    EntryPrice decimal.Decimal

    StrategyName    string
    StrategyVersion string

    Score float64

    Indicators IndicatorSnapshot

    Reasons []string

    CreatedAt time.Time
}
```

The important idea is:

```text
Strategy → Candidate Trade
```

not:

```text
Strategy → LLM
```

The Candidate Trade becomes a stable boundary in the architecture.

---

# 17. Store the Complete Decision Snapshot

Do not only store:

```text
BUY BTCUSDT
```

Store the conditions that produced the decision:

```text
Symbol
Timeframe
Candle timestamp
Entry price
Strategy name
Strategy version
RSI
BB Upper
BB Middle
BB Lower
BB Width
BB %B
RSI slope
Signal score
Reasons
```

This allows you to answer later:

> Why did the bot create this trade?

It also makes debugging, backtesting, RAG, and LLM evaluation much easier.

---

# 18. Strategy Versioning

Every Candidate Trade should contain:

```text
strategy.name
strategy.version
```

Example:

```text
RSI_BB_MEAN_REVERSION
1.0.0

RSI_DIVERGENCE
1.0.0
```

When the strategy changes significantly:

```text
1.0.0 → 1.1.0
```

This is important because backtests must be reproducible.

You should be able to determine:

```text
Which strategy version created this trade?
```

---

# 19. Closed Candle Rule

Only evaluate strategies after a candle is closed.

Correct:

```text
CandleClosed
    ↓
Calculate Indicators
    ↓
Evaluate Strategy
    ↓
Create Candidate Trade
```

Avoid:

```text
Every price tick
    ↓
Recalculate RSI/BB
    ↓
Potentially create signal
```

Using an unfinished candle can cause:

- Signal repainting.
- Different live/backtest results.
- Duplicate signals.
- Unstable decisions.

---

# 20. Prevent Duplicate Candidate Trades

A candidate should be unique for:

```text
Symbol
+
Timeframe
+
Candle Close Time
+
Strategy Version
```

Example idempotency key:

```text
BTCUSDT:5m:2026-09-16T10:35:00Z:RSI_BB_MEAN_REVERSION:1.0.0
```

If the same event is processed twice, the system should not create two Candidate Trades.

This becomes especially important if the system later uses Kafka or background workers.

---

# 21. Candidate Trade Creation Flow

Recommended flow:

```text
CandleClosed
     ↓
IndicatorEngine.Calculate()
     ↓
IndicatorSnapshot
     ↓
Strategy.Evaluate()
     ↓
Signal
     ↓
CandidateTradeFactory
     ↓
CandidateTrade
```

Keep each responsibility separate.

### Indicator Engine

```text
Candle → Indicators
```

### Strategy Engine

```text
Candle + Indicators → Signal
```

### Candidate Trade Factory

```text
Signal + Snapshot → CandidateTrade
```

---

# 22. Unit Tests — Strategy

Create tests for at least these cases:

### Case 1 — No signal

```text
RSI = 50
Close = 100
Lower BB = 95
```

Expected:

```text
No CandidateTrade
```

### Case 2 — RSI oversold only

```text
RSI = 25
Close = 100
Lower BB = 95
```

Expected:

```text
No CandidateTrade
```

Because the price is not below the lower band.

### Case 3 — Price below lower BB only

```text
RSI = 40
Close = 90
Lower BB = 95
```

Expected:

```text
No CandidateTrade
```

### Case 4 — Both conditions satisfied

```text
RSI = 25
Close = 90
Lower BB = 95
```

Expected:

```text
BUY CandidateTrade
```

Reasons:

```text
RSI_OVERSOLD
PRICE_BELOW_BB_LOWER
```

### Case 5 — Boundary conditions

Test:

```text
RSI = 30
RSI = 29.9999

Close = LowerBB
Close = LowerBB - epsilon
```

Define the boundary behavior explicitly.

---

# 23. Integration Tests

Create an end-to-end test:

```text
Candle data
    ↓
Indicator Engine
    ↓
Strategy Engine
    ↓
Candidate Trade
```

Verify that the same historical candle sequence produces the expected Candidate Trade.

Example assertion:

```text
CandidateTrade.Side == BUY
CandidateTrade.StrategyName == RSI_BB_MEAN_REVERSION
CandidateTrade.StrategyVersion == 1.0.0
CandidateTrade.Indicators.RSI ≈ expected RSI
CandidateTrade.Reasons contains RSI_OVERSOLD
CandidateTrade.Reasons contains PRICE_BELOW_BB_LOWER
```

---

# 24. Determinism Test

This is one of the most important tests.

Run the same input multiple times:

```text
Input candles
    ↓
Indicator Engine
    ↓
Strategy Engine
```

Expected:

```text
Result 1 == Result 2 == Result 3
```

There should be no:

- Randomness
- Current-time dependency
- External API dependency
- LLM dependency
- Hidden mutable state

---

# 25. Logging and Observability

Add structured logs around candidate generation.

Example:

```json
{
  "event": "candidate_trade_created",
  "symbol": "BTCUSDT",
  "timeframe": "5m",
  "strategy": "RSI_BB_MEAN_REVERSION",
  "strategyVersion": "1.0.0",
  "side": "BUY",
  "rsi": 27.4,
  "bbLower": 107100,
  "close": 107050
}
```

Do not log excessive raw candle data in every message if it creates unnecessary volume.

---

# 26. Suggested Go Package Structure

```text
internal/
├── marketdata/
│   ├── candle.go
│   ├── exchange.go
│   └── repository.go
│
├── indicator/
│   ├── engine.go
│   ├── config.go
│   ├── rsi.go
│   ├── bb.go
│   ├── divergence.go        ← RSI Divergence detection
│   ├── snapshot.go
│   └── engine_test.go
│
├── strategy/
│   ├── strategy.go
│   ├── context.go
│   ├── signal.go
│   ├── rsibb/
│   │   ├── config.go
│   │   ├── strategy.go
│   │   └── strategy_test.go
│   └── divergence/          ← RSI Divergence strategy
│       ├── config.go
│       ├── strategy.go
│       └── strategy_test.go
│
└── tests/
    └── fixtures/
```

Keep `indicator` and `strategy` independent.

Avoid this dependency:

```text
indicator → strategy
strategy → indicator implementation
```

Prefer:

```text
indicator → produces data
strategy → consumes data
application layer → connects them
```

---

# 27. Recommended Implementation Order

Implement in this exact order:

```text
1. Candle model
       ↓
2. Indicator configuration
       ↓
3. RSI
       ↓
4. Bollinger Bands
       ↓
5. BB Width
       ↓
6. BB %B
       ↓
7. RSI Slope
       ↓
8. RSI Divergence Detection
   (calculateRSISeries + calculateDivergence → DivergenceType in Snapshot)
       ↓
9. Indicator validation
       ↓
10. Strategy interface
       ↓
11. MarketContext
       ↓
12. Signal
       ↓
13. RSI + BB Strategy
       ↓
14. RSI Divergence Strategy
   (reads DivergenceType from Snapshot → BUY/SELL)
       ↓
15. CandidateTrade
       ↓
16. CandidateTradeFactory
       ↓
17. Idempotency
       ↓
18. Unit tests
       ↓
19. Integration tests
       ↓
20. Determinism tests
```

---

# 28. Week 1 Deliverables

By the end of Week 1:

- [ ] Candle domain model implemented.
- [ ] Indicator configuration implemented.
- [ ] RSI(14) implemented.
- [ ] Bollinger Bands(20, 2) implemented.
- [ ] BB Width implemented.
- [ ] BB %B implemented.
- [ ] RSI slope implemented.
- [ ] RSI Divergence Detection implemented (DivergenceType in Snapshot).
- [ ] Indicator unit tests completed.
- [ ] Indicator results validated against a reference.
- [ ] No strategy logic inside Indicator Engine.

### Week 1 Definition of Done

The following should work:

```go
snapshot, err := indicatorEngine.Calculate(candles)
```

and produce a deterministic:

```go
IndicatorSnapshot
```

with validated values.

---

# 29. Week 2 Deliverables

By the end of Week 2:

- [ ] Strategy interface implemented.
- [ ] MarketContext implemented.
- [ ] Signal model implemented.
- [ ] RSI + Bollinger strategy implemented.
- [ ] RSI Divergence Detection implemented (peak/trough + lookback, DivergenceType in Snapshot).
- [ ] RSI Divergence Strategy implemented (BUY on bullish divergence, SELL on bearish divergence).
- [ ] Strategy configuration implemented.
- [ ] CandidateTrade model implemented.
- [ ] CandidateTradeFactory implemented.
- [ ] Strategy versioning implemented (RSI_BB_MEAN_REVERSION v1.0.0, RSI_DIVERGENCE v1.0.0).
- [ ] Closed-candle validation implemented.
- [ ] Duplicate candidate prevention implemented.
- [ ] RSI+BB strategy unit tests completed.
- [ ] RSI Divergence strategy unit tests completed.
- [ ] End-to-end integration test completed.
- [ ] Determinism test completed.
- [ ] Structured logging implemented.

### Week 2 Definition of Done

The following pipeline should work reliably for both strategies:

```text
Closed Candle
     ↓
Indicator Engine
     ↓
Indicator Snapshot (RSI, BB, DivergenceType, …)
     ↓
RSI + BB Strategy  ─────┐
                         ├── Signal → Candidate Trade
Divergence Strategy ────┘
```

For identical input data, the output must be identical.

---

# 30. Final Architecture After Week 2

```text
                    ┌─────────────────────┐
                    │     Market Data     │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │       Candle        │
                    │    Closed Candle    │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │   Indicator Engine  │
                    │                     │
                    │ RSI                 │
                    │ Bollinger Bands     │
                    │ BB Width            │
                    │ BB %B               │
                    │ RSI Slope           │
                    │ RSI Divergence      │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ Indicator Snapshot  │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │   Strategy Engine   │
                    │                     │
                    │ RSI + BB Strategy   │
                    │ Divergence Strategy │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │       Signal        │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ Candidate Trade     │
                    │                     │
                    │ Strategy + Version  │
                    │ Indicators          │
                    │ Entry Price         │
                    │ Side                │
                    │ Score               │
                    │ Reasons             │
                    └──────────┬──────────┘
                               │
                               ▼
                    Future: Risk Engine
                               │
                               ▼
                    Future: LLM Risk Manager
```

## Architectural Principles

### 1. Deterministic First

The same input must produce the same output.

### 2. Separation of Concerns

```text
Indicator Engine → What does the market data say?
Strategy Engine  → Does it match our trading rules?
Risk Engine      → Is the trade allowed?
LLM              → Should risk be adjusted based on broader context?
Execution        → How should the approved trade be executed?
```

### 3. No LLM in the Core Signal Path

The LLM should not generate the initial BUY/SELL signal.

The deterministic strategy should create the Candidate Trade first.

### 4. Preserve the Decision Context

Every Candidate Trade should contain enough information to reconstruct why it was created.

### 5. Version Everything

At minimum:

```text
Strategy version
Indicator configuration
```

This is required for reproducible backtesting.

### 6. Build the Baseline Before AI

Before introducing RAG or an LLM, establish:

```text
Indicator
    +
Strategy
    +
Backtest
    =
Baseline Performance
```

Only then can you measure whether AI actually improves the trading system.
