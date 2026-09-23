Status: ready-for-agent

# Spec: Week 1–2 Indicator Engine, Strategy Engine, and Candidate Trade Pipeline

---

## Problem Statement

The trading system has no mechanism to detect potential entry opportunities from market data. There is no candle model, no indicator calculation layer, and no strategy evaluation layer. Without these, the system cannot generate the `CandidateTrade` records that will feed the future AI Risk Manager. The baseline deterministic signal pipeline — the foundation that all AI components will build on — does not yet exist.

---

## Solution

Build a deterministic, testable signal pipeline as a new `trading_bot` Go module. The pipeline consumes closed candles from KuCoin, calculates RSI, Bollinger Band, and RSI Divergence indicators, evaluates the RSI Divergence strategy, and persists `CandidateTrade` records to PostgreSQL. For identical candle input, the pipeline must always produce identical output. This establishes the stable contract boundary between the deterministic strategy layer and the future AI Risk Manager.

---

## User Stories

1. As a developer, I want a `Candle` domain model independent of any exchange format, so that the indicator and strategy engines are not coupled to KuCoin's API response structure.
2. As a developer, I want a `CandleRepository` interface with a file-based implementation, so that indicator and strategy tests run offline against fixture data without hitting KuCoin.
3. As a developer, I want a KuCoin REST adapter that fetches closed candles and normalizes symbols from `BTC-USDT` to `BTCUSDT`, so that the domain layer is exchange-agnostic.
4. As a developer, I want RSI(14) calculated using Wilder's smoothing (RMA), so that the output matches TradingView's RSI values within ±0.0001.
5. As a developer, I want Bollinger Bands(20, 2) calculated using population standard deviation, so that the output matches TradingView's BB values within ±0.0001.
6. As a developer, I want BB Width, BB %B, and RSI Slope calculated alongside Bollinger Bands, so that all indicator features are available to the strategy in a single `IndicatorSnapshot`.
7. As a developer, I want an `IndicatorEngine` interface that takes candles and returns an `IndicatorSnapshot`, so that the strategy engine is decoupled from indicator implementation details.
8. As a developer, I want the `IndicatorEngine` to require at least 1000 candles as input, so that RSI is sufficiently stabilized before being used for signal generation.
9. As a developer, I want a `Strategy` interface with an `Evaluate(MarketContext)` method, so that multiple strategies can be swapped in without changing the pipeline.
10. As a developer, I want a `MarketContext` struct containing the closed candle and its `IndicatorSnapshot`, so that strategies receive everything they need without recalculating indicators.
11. As a developer, I want a directional scoring model applied after signal detection, so that the strength of the signal is quantified for downstream consumers.
12. As a developer, I want RSI Slope direction to contribute ±15 to the score, so that a confirming slope adds confidence while a counter slope reduces it.
13. As a developer, I want a `Signal` struct with `Side`, `Score`, and `Reasons []string`, so that downstream consumers know both the direction and the rationale for the signal.
14. As a developer, I want `Reasons` to contain named constants (e.g. `RSI_BULLISH_DIVERGENCE`, `RSI_BEARISH_DIVERGENCE`), so that decision context is human-readable and machine-parseable.
15. As a developer, I want a `CandidateTrade` struct that captures the full decision snapshot (symbol, timeframe, side, entry price, strategy name, strategy version, all indicator values, score, reasons, idempotency key), so that any future consumer can reconstruct why the trade was proposed.
16. As a developer, I want `EntryPrice` set to the close price of the triggering candle, so that the value is deterministic and requires no additional API call.
17. As a developer, I want a `CandidateTradeFactory` that generates a UUID and idempotency key from `Symbol:Timeframe:CloseTime:StrategyName:StrategyVersion`, so that duplicate detection is built into the creation contract.
18. As a developer, I want the pipeline to only evaluate the strategy on closed candles, so that signal repainting and duplicate signals from in-progress candles are prevented.
19. As a developer, I want `CandidateTrade` records persisted to a `trading_bot` PostgreSQL database with a `UNIQUE` constraint on the idempotency key, so that duplicate signals are prevented across process restarts.
20. As a developer, I want the `INSERT` to use `ON CONFLICT (idempotency_key) DO NOTHING`, so that idempotent re-processing is safe without error-handling overhead.
21. As a developer, I want a structured `slog` log event emitted only when a new (non-duplicate) `CandidateTrade` is created, so that observability is accurate without excessive noise.
22. As a developer, I want the polling loop to run every 5 minutes, fetching 1000 closed candles per symbol per timeframe from KuCoin, so that the pipeline is stateless and RSI is always sufficiently warm.
23. As a developer, I want both `1hour` and `4hour` timeframes active from day one, so that the pipeline produces signals across multiple market contexts.
24. As a developer, I want the list of monitored symbols to be configurable (default: `["BTCUSDT"]`), so that adding new symbols requires only a config change.
25. As a developer, I want all indicator configuration (RSI period, BB period, BB multiplier, RSI slope period) to be part of a versioned config struct, so that backtests are reproducible by replaying the same config.
26. As a developer, I want every `CandidateTrade` to store `StrategyName` and `StrategyVersion` (e.g. `RSI_DIVERGENCE`, `1.0.0`), so that backtests can attribute each trade to an exact strategy version.
27. As a developer, I want the `Side` type to support both `BUY` and `SELL` from day one, so that adding SELL signal generation in a future sprint requires no domain model changes.
28. As a developer, I want indicator values validated against TradingView reference data within ±0.0001 tolerance, so that the implementation is provably correct before strategy logic is built on top.
29. As a developer, I want an integration test that runs the full pipeline with fixture candles and asserts the resulting `CandidateTrade` fields, so that regressions in the end-to-end path are caught automatically.
30. As a developer, I want a determinism test that runs the same fixture candles through the pipeline twice and asserts that all meaningful output fields are identical, so that hidden mutable state or time-dependency bugs are caught.
31. As a developer, I want `calculateRSISeries()` to return the full RSI time series as `[]float64` (one value per candle from index `RSIPeriod`), so that divergence detection can scan historical RSI peaks and troughs without re-computing RSI.
32. As a developer, I want `IndicatorSnapshot` to include a `DivergenceType` field (None / Bearish / Bullish), so that strategies can consume pre-computed divergence state without accessing raw candle history directly.
33. As a developer, I want divergence detection parameters (`PeakThreshold=68`, `TroughThreshold=32`, `LookbackCandles=20`) to live in `IndicatorConfig.Divergence`, so that detection behaviour is versioned alongside other indicator configuration and matches TradingView defaults.
34. As a developer, I want `calculateDivergence()` to identify a bearish divergence when the current candle is an RSI peak (RSI > 68, strict) AND a previous peak within `LookbackCandles` has higher RSI but lower price high, so that price higher-high / RSI lower-high patterns are detected.
35. As a developer, I want `calculateDivergence()` to identify a bullish divergence when the current candle is an RSI trough (RSI < 32, strict) AND a previous trough within `LookbackCandles` has lower RSI but higher price low, so that price lower-low / RSI higher-low patterns are detected.
36. As a developer, I want the RSI Divergence strategy to generate a BUY signal on `DivergenceBullish` and a SELL signal on `DivergenceBearish`, so that both bullish and bearish momentum-divergence patterns produce actionable candidates.
37. As a developer, I want divergence scoring to use a base of +50 with RSI Slope contributing ±15 (confirming direction +15, counter direction -15), so that signal strength reflects both the divergence structure and momentum confirmation.
38. As a developer, I want `RSI_BULLISH_DIVERGENCE` and `RSI_BEARISH_DIVERGENCE` as named reason constants, so that downstream consumers can identify divergence-sourced signals programmatically.
39. As a developer, I want `calculateDivergence()` edge cases tested with synthetic RSI series (no divergence, bearish pattern, bullish pattern, prior peak/trough outside lookback window, RSI exactly at threshold), so that divergence detection correctness is independently verifiable without relying on the fixture candle set containing a divergence.
40. As a developer, I want a Seam 3 integration test for `DivergenceStrategy`, wiring `FileCandleRepository → IndicatorEngine → DivergenceStrategy → CandidateTradeFactory`, so that the end-to-end pipeline is verified.
41. As a developer, I want the RSI Divergence strategy versioned as `StrategyName = "RSI_DIVERGENCE"`, `StrategyVersion = "1.0.0"`, so that its `CandidateTrade` records are attributable to this exact strategy implementation.

---

## Implementation Decisions

- **New Go module**: `trading_bot` added to the existing `go.work` workspace. It does not share a module with `auth`, `email_consumer`, or other services.

- **PostgreSQL connection budget**: The `trading_bot` module will call `OpenPostgres` from `golang/shared/database/postgres.go`. Before wiring it up, verify that `(existing_service_count + 1) × 25 ≤ max_connections - 10` and adjust `max_connections` in `credentials/qex/docker-compose.yml` if needed. Currently 4 services × 25 = 100; adding `trading_bot` = 5 × 25 = 125, within the 200 limit.

- **Separate `trading_bot` database**: Uses the same PostgreSQL instance as `auth` but a dedicated database, keeping trading and auth schemas completely isolated.

- **sqlc for queries**: All SQL interaction uses sqlc-generated code, consistent with the `auth` module pattern.

- **golang-migrate for migrations**: Schema migrations use `golang-migrate`, consistent with the `auth` module pattern.

- **No external decimal library**: All numeric types use `float64`. KuCoin returns prices as strings; the KuCoin adapter parses them with `strconv.ParseFloat`. Indicator values are `float64` throughout. This is sufficient for signal generation (not financial settlement).

- **Symbol normalization at the adapter boundary**: The KuCoin adapter translates `BTC-USDT` → `BTCUSDT`. All domain types, idempotency keys, logs, and DB records use the canonical format.

- **RSI algorithm**: Wilder's Smoothing (RMA). Seeded with SMA of the first `RSIPeriod` gains/losses. Subsequent values: `avg = (prev × (period-1) + current) / period`. When `avgLoss == 0`, RSI = 100. Returns the full RSI time series as `[]float64` — one value per candle starting from index `RSIPeriod`. The last element is used as `IndicatorSnapshot.RSI` (no change to the public interface). The full series is passed internally to `calculateDivergence()` for peak/trough scanning.

- **Bollinger Bands**: Population standard deviation (`/ N`, not `/ N-1`). This matches TradingView exactly. `BBPercentB` handles the `Upper == Lower` edge case by returning 0.5.

- **Directional scoring (Divergence strategy)**:
  - Divergence detected (base): +50
  - RSI Slope confirming direction (BUY: slope > 0; SELL: slope < 0): +15
  - RSI Slope counter to direction: -15
  - Score range: 35–65. No additional tiers in v1.0.0.

- **RSI Divergence detection**: Bearish divergence is detected when the last closed candle is an RSI peak (strict `RSI > 68`) AND a previous peak within `LookbackCandles=20` exists where `prevRSI > curRSI` AND `prevHigh < curHigh`. Bullish divergence is detected when the last closed candle is an RSI trough (strict `RSI < 32`) AND a previous trough where `prevRSI < curRSI` AND `prevLow > curLow`. Bearish is checked first; if found, bullish check is skipped. Detection parameters live in `IndicatorConfig.Divergence` — not in the strategy config — because detection is an indicator-layer computation stamped into `Snapshot.DivergenceType`.

- **Divergence configuration location**: `PeakThreshold`, `TroughThreshold`, and `LookbackCandles` live in `IndicatorConfig.Divergence`, not in a strategy config. This preserves the `indicator → produces data, strategy → consumes data` boundary: the strategy reads a pre-computed `DivergenceType` from `Snapshot` and never controls how detection is performed.

- **Idempotency key format**: `BTCUSDT:1hour:2026-09-16T10:00:00Z:RSI_DIVERGENCE:1.0.0` (canonical symbol, timeframe as configured string, candle `CloseTime` in RFC3339 UTC, strategy name, strategy version).

- **`EntryPrice`**: Set to `Candle.Close` of the candle that triggered the signal. No additional API call required.

- **Polling**: Stateless. Every 5 minutes, fetch the last 1000 closed candles from KuCoin per symbol × timeframe. The full 1000-candle history is re-fetched each time; no incremental state is maintained in memory.

- **Post-creation action**: `INSERT ... ON CONFLICT DO NOTHING`. If rows affected = 1, emit a structured `slog` event. If rows affected = 0, it is a duplicate — skip silently.

- **Strategy versioning**: `RSI_DIVERGENCE / 1.0.0` is the sole strategy in this sprint. Version bumps when strategy logic or scoring model changes. Never reuse a version string for a changed strategy.

---

## Testing Decisions

A good test verifies observable contracts at the right seam — not internal math, not private functions. Tests should be fast, offline (no KuCoin, no real DB), and deterministic.

**Seam 1 — `IndicatorEngine.Calculate(candles []Candle) (IndicatorSnapshot, error)`**
- Tests pass fixture candles (committed JSON file with ~200 BTCUSDT 1h candles from TradingView).
- Assert every field of `IndicatorSnapshot` matches TradingView reference values within ±0.0001.
- Assert `DivergenceType` field — whatever value the fixture candles produce is the reference value. Do not force the fixture to contain a divergence.
- Do NOT have separate unit tests for RSI math or BB math in isolation — only test through this interface unless a specific mathematical edge case (`avgLoss == 0`, `Upper == Lower`) genuinely cannot be exercised through the public seam.
- `calculateDivergence()` edge cases (no divergence, bearish pattern, bullish pattern, outside lookback, exact-threshold RSI values) must be tested via a dedicated `internal/indicator/divergence_test.go` using synthetic candle slices and RSI series — these patterns cannot be reliably constructed through the public seam.
- Edge cases to cover: insufficient candle count (< BBPeriod), constant prices, all-up prices, all-down prices, volatile prices.

**Seam 2 — `DivergenceStrategy.Evaluate(ctx MarketContext) (*Signal, error)`**
- Tests construct `MarketContext` directly with a hardcoded `IndicatorSnapshot.DivergenceType` — no candles, no engine.
- Required cases: `DivergenceNone` → nil; `DivergenceBullish` + slope > 0 → BUY, score=65, `RSI_BULLISH_DIVERGENCE`; `DivergenceBullish` + slope ≤ 0 → BUY, score=35; `DivergenceBearish` + slope < 0 → SELL, score=65, `RSI_BEARISH_DIVERGENCE`; `DivergenceBearish` + slope ≥ 0 → SELL, score=35.
- Determinism sub-test: same `MarketContext` input twice → identical `Side`, `Score`, `Reasons`.

**Seam 3 — Full pipeline integration (DivergenceStrategy, via fake `CandleRepository`)**
- Uses a `FileCandleRepository` that loads fixture candles from disk. No real KuCoin call. No real database.
- Wires: `FileCandleRepository` → `IndicatorEngine` → `DivergenceStrategy` → `CandidateTradeFactory`.
- If fixture candles do not produce a divergence on the last candle: assert `nil` signal and no `CandidateTrade` created.
- Determinism sub-test: run same fixture twice, assert `DivergenceType`, `Side`, `EntryPrice`, `StrategyName`, `StrategyVersion`, `Score`, `Reasons` are identical. Exclude `Id` and `CreatedAt`.

Prior art: `auth_bot` uses `httptest.Server` stubs and `testify` assertions. Seam 3 requires no DB so `testcontainers` is not needed.

---

## Out of Scope

- LLM / AI Risk Manager, RAG, ML prediction models
- Live order execution or real-money trading
- Advanced market-regime classification, portfolio optimization, dynamic strategy selection
- Multi-symbol validation (only BTCUSDT validated against TradingView in this sprint)
- KuCoin WebSocket subscription (polling only for this sprint)
- RabbitMQ publishing of `CandidateTrade` (Week 3+ when Risk Engine exists to consume it)

---

## Vertical Slices

Each slice is independently deliverable, has a clear done condition, and leaves the system in a working state. Slices are ordered by dependency — each one builds on the previous.

---

### Slice 1 — Module scaffold + Candle model

Deliverable: `trading_bot` Go module exists in `go.work`, with a `Candle` struct, a `CandleRepository` interface, and a `FileCandleRepository` that loads fixture candles from a JSON file.

Done when:
- `go build ./...` passes with zero errors
- A fixture JSON file exists with at least 200 BTCUSDT 1h candles
- `FileCandleRepository.GetClosedCandles()` returns those candles correctly

No indicator logic. No strategy. No database.

---

### Slice 2 — RSI calculation

Deliverable: `IndicatorEngine.Calculate()` returns a correct `RSI` value in `IndicatorSnapshot`. All other snapshot fields are zero.

Done when:
- Wilder's smoothing is implemented (seed with SMA, then RMA)
- `avgLoss == 0` edge case returns RSI = 100
- `< RSIPeriod + 1` candles returns an error
- `calculateRSISeries()` returns the full `[]float64` series; `series[len-1]` equals the scalar RSI value
- RSI value from fixture candles matches TradingView reference within ±0.0001
- Seam 1 tests pass for RSI field

---

### Slice 3 — Bollinger Bands + derived metrics

Deliverable: `IndicatorEngine.Calculate()` returns a fully populated `IndicatorSnapshot` (RSI, BBUpper, BBMiddle, BBLower, BBWidth, BBPercentB, RSISlope). `DivergenceType` field exists in the struct but is populated in the next slice.

Done when:
- Population std dev is used (matches TradingView)
- `Upper == Lower` edge case in BBPercentB returns 0.5
- `< BBPeriod` candles returns an error
- All snapshot fields from fixture candles match TradingView reference within ±0.0001
- Full Seam 1 test suite passes

---

### Slice 4 — RSI Divergence Detection

Deliverable: `IndicatorEngine.Calculate()` returns a fully populated `IndicatorSnapshot` including `DivergenceType` (None / Bearish / Bullish), computed from the full RSI series and candle price history.

Done when:
- `calculateDivergence(candles, rsiSeries, cfg)` is a pure function in `internal/indicator/divergence.go`
- Bearish: last candle is RSI peak (strict > 68) AND previous peak within 20 candles has higher RSI + lower price high
- Bullish: last candle is RSI trough (strict < 32) AND previous trough within 20 candles has lower RSI + higher price low
- Bearish checked first; bullish skipped if bearish found
- `internal/indicator/divergence_test.go` covers: no divergence, bearish pattern, bullish pattern, outside lookback, RSI exactly at threshold (no detection at 68.0 / 32.0)
- Full Seam 1 test suite passes including `DivergenceType` assertion on fixture candles

---

### Slice 5 — RSI Divergence Strategy (BUY + SELL)

Deliverable: `DivergenceStrategy.Evaluate()` returns a `*Signal` with correct `Side`, `Score`, and `Reasons` when `DivergenceType != None`; returns `nil` otherwise.

Done when:
- `DivergenceBullish` → `SideBuy`, reason `RSI_BULLISH_DIVERGENCE`, base score +50
- `DivergenceBearish` → `SideSell`, reason `RSI_BEARISH_DIVERGENCE`, base score +50
- RSI Slope confirming direction → +15; counter direction → -15
- `StrategyName = "RSI_DIVERGENCE"`, `StrategyVersion = "1.0.0"`
- All Seam 2b cases pass (6 cases including determinism)
- `internal/strategy/divergence/` package exists with `config.go`, `strategy.go`, `strategy_test.go`

---

### Slice 6 — CandidateTrade + factory + in-memory idempotency

Deliverable: `CandidateTradeFactory.Create()` produces a `CandidateTrade` from a `Signal` + `Candle` + `IndicatorSnapshot`, with UUID, idempotency key, and all fields populated. An in-memory map prevents duplicate creation within a single process run.

Done when:
- Idempotency key is `Symbol:Timeframe:CloseTime:StrategyName:StrategyVersion`
- Calling factory twice with same inputs returns the trade only once (second call returns nil)
- Full Seam 3 integration test passes (DivergenceStrategy)
- Determinism sub-test passes (same fixture → same meaningful fields twice)

---

### Slice 7 — PostgreSQL persistence

Deliverable: `CandidateTrade` is inserted into the `trading_bot` PostgreSQL database. Duplicate inserts (same idempotency key) are silently ignored. A structured `slog` event is emitted only on a new insert.

Done when:
- golang-migrate migration creates the `candidate_trades` table with `UNIQUE (idempotency_key)`
- sqlc-generated `InsertCandidateTrade` query uses `ON CONFLICT DO NOTHING`
- Re-processing the same candle produces exactly one DB row
- `slog` event fires on first insert, silent on duplicate

---

### Slice 8 — KuCoin REST adapter

Deliverable: `KuCoinAdapter` implements `CandleRepository`, fetches the last 1000 closed candles for a given symbol + timeframe, and normalizes `BTC-USDT` → `BTCUSDT`.

Done when:
- Symbol normalization is applied at the adapter boundary
- Response is parsed correctly (KuCoin returns newest-first; adapter reverses to oldest-first)
- `IsClosed` is set correctly (only fully closed candles are returned)
- Adapter can be replaced by `FileCandleRepository` in tests with no other code changes

---

### Slice 9 — Polling runner + binary

Deliverable: `cmd/main.go` wires everything together and starts a polling loop that fires every 5 minutes per symbol × timeframe, running the full pipeline and persisting results.

Done when:
- Config is loaded from JSON (symbols, timeframes, poll interval, indicator config, strategy config, DB DSN, KuCoin URL)
- Graceful shutdown on `SIGINT`/`SIGTERM`
- End-to-end manual test: start the binary, observe `candidate_trade_created` log events and DB rows

---

## Further Notes

- The `CandidateTrade` is the stable architectural boundary. Everything upstream (indicators, strategy) is deterministic and testable. Everything downstream (Risk Engine, LLM) consumes this contract without needing to know how it was generated.
- The fixture file for TradingView validation should be committed with the exact candle data used to derive expected indicator values, so validation is fully reproducible.
- When `StrategyVersion` changes (e.g. `1.0.0` → `1.1.0`), existing `CandidateTrade` records remain attributable to their original version. Never reuse a version string for a changed strategy.
- The idempotency key uses `CloseTime` (not `OpenTime`) as the candle identifier, consistent with the closed-candle rule.
- The RSI Divergence strategy (`RSI_DIVERGENCE / 1.0.0`) scoring model (base +50, slope ±15, range 35–65) is intentionally minimal. Scoring tiers should only be added after backtest data is available to justify them.
- `DivergenceConfig` parameters (`PeakThreshold=68`, `TroughThreshold=32`, `LookbackCandles=20`) match the C# `FindSignalCommand` implementation values exactly, validated against that production system.
