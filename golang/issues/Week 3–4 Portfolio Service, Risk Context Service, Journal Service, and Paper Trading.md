Status: ready-for-agent

# Spec: Week 3–4 Portfolio Service, Risk Context Service, Journal Service, and Paper Trading

---

## Problem Statement

The trading system can generate `CandidateTrade` records, but nothing happens after they are persisted. There is no portfolio awareness (balance, positions, exposure), no risk evaluation before acting on a signal, no record of what happened to each candidate, and no protection against trading into an overexposed or loss-limited state. The system cannot answer: "Was this candidate safe to act on? What happened next? Why?" Without this infrastructure, the AI Risk Manager (Week 7–8) has nothing structured to evaluate and no history to reason over.

The original design put all post-candidate logic into a single `processcandidatetrade` handler. This was too coarse: risk evaluation and order lifecycle are distinct concerns. Risk evaluation is signal-triggered; order creation and fill detection are interval-triggered. Keeping them in one handler prevented independent reasoning and testing of each concern.

---

## Solution

Extend the `trading_bot` binary with two new internal packages (`risk`, `portfolio`) and four focused application handlers. When a `CandidateTrade` is created by the Strategy Engine:

1. **`risk/evaluate`** — triggered synchronously after `find`, assembles portfolio context, evaluates the hard risk gate, and persists the risk decision.
2. **`order/create`** — runs every pipeline tick, finds approved risk decisions without an order, and creates an `OPEN` paper order for each.
3. **`order/fill`** — runs every pipeline tick, checks OPEN paper orders against 15-minute market data, and fills orders whose price has been crossed.
4. **`order/close`** — triggered on demand via HTTP API (`POST /orders/{paper_order_id}/close`), fetches the current exit price from KuCoin, writes a `TradeOutcome`, and closes the position.

All of this runs in paper trading mode — no real orders, no real money — producing a realistic lifecycle record that will feed the AI Risk Manager and RAG service in later weeks.

---

## Architecture

### Handler registry

| Handler | Package | Trigger | Key dependencies |
|---|---|---|---|
| `candidatetrade/find` | `application/candidatetrade/find` | Per symbol×timeframe goroutine | `kucoin.Service`, `indicator.Engine`, `strategy.Strategy`, `generated.Queries` |
| `risk/evaluate` | `application/risk/evaluate` | Inline after `find` when `TradeId != ""` | `risk.RiskContextService`, `risk.HardRiskGate`, `generated.Queries` |
| `portfolio/sync` | `application/portfolio/sync` | Once on startup | `kucoin.Service`, `generated.Queries` |
| `order/create` | `application/order/create` | Once per tick after symbol×timeframe goroutines | `generated.Queries` |
| `order/fill` | `application/order/fill` | Once per tick after `order/create` | `kucoin.Service`, `generated.Queries` |
| `order/close` | `application/order/close` | HTTP API: `POST /orders/{id}/close` | `kucoin.Service`, `generated.Queries` |

### Pipeline sequence per tick

```
[startup]
  portfolio/sync.handler.Handle()      → sync KuCoin balances (non-fatal)

[each tick]
  ┌─ goroutine per symbol×timeframe ──────────────────────────────┐
  │  candidatetrade/find.handler.Handle()   → Result{TradeId}     │
  │  if TradeId != "":                                            │
  │    risk/evaluate.handler.Handle()       → Result{DecisionId}  │
  └───────────────────────────────────────────────────────────────┘
  wg.Wait()

  order/create.handler.Handle()        → (creates OPEN paper orders)
  order/fill.handler.Handle()          → (fills crossed orders)

[HTTP API — on demand]
  POST /orders/{paper_order_id}/close
    → order/close.handler.Handle(Command{PaperOrderId})
```

### Journal FK chain

```
candidate_trades
  └── portfolio_snapshots   (candidate_id FK)
  └── risk_contexts         (candidate_id FK, portfolio_snapshot_id FK)
  └── risk_decisions        (candidate_id FK, risk_context_id FK)
  └── paper_orders          (candidate_id FK, risk_decision_id FK)
        └── paper_fills     (candidate_id FK, paper_order_id FK)
              └── trade_outcomes (candidate_id FK, paper_fill_id FK)
  └── positions             (candidate_id FK)
```

---

## User Stories

1. As a developer, I want a `syncportfolio` handler that calls `kucoin.Service.GetBalances()` on application startup and writes fresh balances to the DB, so that the internal DB reflects the real account state before the first pipeline run.
2. As a developer, I want `kucoin.Service.GetBalances()` and `kucoin.Service.GetOpenOrders()` to use KuCoin V2 Signature authentication via credentials stored in `kucoin.Config`, so that authenticated exchange calls are centralised in the kucoin service alongside the existing `GetClosedCandles`.
3. As a developer, I want internal portfolio state stored in PostgreSQL tables (`account_balances`, `portfolio_snapshots`, `positions`), so that state survives restarts and is available for risk context assembly without hitting KuCoin on every signal.
4. As a developer, I want KuCoin to be the source of truth — on any reconciliation conflict, exchange data wins — so that internal state never diverges silently from reality.
5. As a developer, I want a `PortfolioSnapshot` captured at the moment a `CandidateTrade` is evaluated, so that the exact portfolio state used in every risk decision is preserved immutably for audit and RAG.
6. As a developer, I want exposure calculated as `Quantity × CurrentPrice` per asset and summed to total exposure, so that the risk gate can compare candidate exposure against portfolio-level limits.
7. As a developer, I want a `RiskContext` struct that aggregates `CandidateTrade`, `PortfolioSnapshot`, `PositionRiskContext`, `TradingHistoryContext`, `RiskLimits`, and `MarketRiskContext` into a single serializable envelope, so that the future LLM Risk Manager receives all relevant facts without querying any database or service directly.
8. As a developer, I want a `RiskContextService` that assembles `RiskContext` from portfolio and journal data, so that context assembly is a single testable operation with no side effects on the caller.
9. As a developer, I want `TradingHistoryContext` to include recent win/loss count, daily PnL, and consecutive losses derived from journal data, so that the risk gate and future LLM can factor in recent strategy performance.
10. As a developer, I want `RiskLimits` to be fully config-driven (max daily loss, max portfolio exposure, max symbol exposure, max open positions, max consecutive losses), so that risk thresholds can be changed without code changes.
11. As a developer, I want a `HardRiskGate` that evaluates a `RiskContext` against `RiskLimits` and returns a `RiskDecision` (APPROVE / REJECT), so that every candidate is evaluated against deterministic hard limits before any AI involvement.
12. As a developer, I want the `HardRiskGate` to reject on daily loss limit exceeded, so that the bot stops trading after a configured drawdown threshold.
13. As a developer, I want the `HardRiskGate` to reject on total portfolio exposure limit exceeded, so that the bot never over-allocates capital across all open positions.
14. As a developer, I want the `HardRiskGate` to reject on symbol exposure limit exceeded, so that the bot never concentrates too much capital in a single asset.
15. As a developer, I want the `HardRiskGate` to reject when open positions are at the configured maximum, so that position count is bounded.
16. As a developer, I want the `HardRiskGate` to reject on consecutive losses exceeding the configured threshold, so that the bot pauses after a losing streak.
17. As a developer, I want the `HardRiskGate` to reject on insufficient available balance, so that orders are never attempted without enough capital.
18. As a developer, I want the `HardRiskGate` to be a pure function with no database or network calls, so that it is fast, fully unit-testable, and deterministic.
19. As a developer, I want the `HardRiskGate` to run both before and after the LLM Risk Manager (double-gate), so that the LLM cannot override hard limits even if it returns APPROVE on a disqualified candidate.
20. As a developer, I want a `risk/evaluate` application handler that receives a `TradeId`, assembles the risk context, evaluates the hard gate, and persists a `RiskDecision` record, so that risk logic is a single-responsibility handler separate from order lifecycle.
21. As a developer, I want `risk/evaluate` to be triggered synchronously from `runPipeline` immediately after `find.handler` returns a non-empty `TradeId`, so that every new candidate is risk-evaluated in the same pipeline tick it was created.
22. As a developer, I want a `RiskDecision` record persisted to the journal as an immutable insert immediately after the `HardRiskGate` evaluates, so that every decision — including REJECTs — is traceable.
23. As a developer, I want `RiskDecision.Reasons` to contain named string constants (e.g. `DAILY_LOSS_LIMIT_EXCEEDED`, `INSUFFICIENT_BALANCE`), so that rejection reasons are human-readable and machine-parseable.
24. As a developer, I want an `order/create` application handler that runs every pipeline tick, queries approved risk decisions that have no corresponding paper order, and creates an `OPEN` paper order for each, so that order creation is decoupled from risk evaluation.
25. As a developer, I want `order/create` to query approved decisions without orders using a single JOIN across `risk_decisions`, `paper_orders`, and `candidate_trades`, so that the handler has everything it needs (symbol, side, entry price) without a second round-trip.
26. As a developer, I want an `order/fill` application handler that runs every pipeline tick, gets all OPEN paper orders, and checks each one against 15-minute KuCoin candles to determine if the order price has been crossed, so that fill detection is realistic and market-aware.
27. As a developer, I want `order/fill` to always use the 15-minute timeframe for fill checking, regardless of the signal's original timeframe, so that fill detection is responsive without depending on a JOIN to retrieve the candidate's timeframe.
28. As a developer, I want `order/fill` to consider an order filled when any 15-minute candle since `order.CreatedAt` has `Low ≤ FillPrice` (BUY) or `High ≥ FillPrice` (SELL), so that fill simulation is consistent with limit-order price-cross semantics.
29. As a developer, I want the fill price to be the order's original `FillPrice` (= candidate's entry price), so that no additional API call is required and fill price is deterministic.
30. As a developer, I want OPEN orders without a price-cross to remain OPEN indefinitely without expiry, so that the system stays simple while position count limits prevent stale orders from accumulating.
31. As a developer, I want `order/fill` to write `UpdatePaperOrderStatus` → `FILLED`, `InsertPaperFill`, and `UpsertPosition` when a fill is detected, so that the full paper trade lifecycle is recorded.
32. As a developer, I want `order/create` and `order/fill` to run on the same ticker interval as the main pipeline (once per tick, after all symbol×timeframe goroutines complete), so that no second ticker or additional infrastructure is needed.
33. As a developer, I want all journal records linked by a `candidate_id` foreign key chain, so that the complete lifecycle of any trade is reconstructable from a single candidate ID.
34. As a developer, I want all journal tables to use `ON CONFLICT DO NOTHING` on their natural business key, so that idempotent re-processing never creates duplicate records.
35. As a developer, I want structured `slog` log events emitted at each pipeline stage (`risk_context_created`, `risk_decision_created`, `risk_decision_rejected`, `paper_order_created`, `paper_order_filled`, `trade_outcome_created`) with `candidate_id` in every event, so that the full lifecycle is traceable in logs.
36. As a developer, I want the portfolio sync to be non-fatal on startup failure — proceeding with whatever state is in the DB — so that the bot continues to paper-trade even if KuCoin is temporarily unreachable.
37. As a developer, I want spot trading only in Week 3–4 (no leverage, no margin, no futures), so that the `Position` model stays simple and the risk calculations have no liquidation complexity.
38. As a developer, I want all new packages (`portfolio`, `risk`, `order`) to be internal to the `trading_bot` module, with all DB writes going through `generated.Queries` directly, so that data ownership is explicit and the system remains a single deployable binary.
39. As a developer, I want an `order/close` handler that accepts a `paper_order_id`, fetches the current exit price from KuCoin (latest 15-minute candle close), writes a `TradeOutcome` record, and sets the position quantity to zero, so that trade performance can be measured and `TradingHistoryContext` statistics become meaningful.
40. As a developer, I want `order/close` to be triggered via an HTTP API endpoint (`POST /orders/{paper_order_id}/close`), so that positions can be closed on demand without automated logic.
41. As a developer, I want `TradeOutcome` to record `EntryPrice`, `ExitPrice`, `Quantity`, `GrossPnL`, `Fees`, `NetPnL`, and `ClosedAt`, so that downstream consumers (RAG, LLM) can reconstruct strategy performance from journal data.
42. As a developer, I want fees set to zero in the `TradeOutcome` for Week 3–4, so that the model remains simple until a fee structure is defined.
43. As a developer, I want `GrossPnL` computed as `(ExitPrice - EntryPrice) × Quantity` for BUY and `(EntryPrice - ExitPrice) × Quantity` for SELL, so that PnL direction is correct for both sides.
44. As a developer, I want the HTTP server to listen on a configurable port (default `8080`) and shut down gracefully when the main context is cancelled, so that the API lifecycle is tied to the binary lifecycle.
45. As a developer, I want `trade_outcomes` linked by `candidate_id` and `paper_fill_id` foreign keys, so that each outcome is traceable to the exact fill that opened the position.

---

## Implementation Decisions

- **Four focused handlers replace one**: The original `candidatetrade/process` handler is deleted entirely. Its responsibilities are redistributed: risk evaluation to `risk/evaluate`, order creation to `order/create`, fill detection and execution to `order/fill`, position close and outcome recording to `order/close`. Each handler has a single trigger and a single responsibility.

- **`risk/evaluate` is synchronous**: `runPipeline` calls `riskEvaluateHandler.Handle(ctx, Command{TradeId})` inline after `find.handler` returns a non-empty `TradeId`, within the same goroutine. No message queue, no channels — same call pattern as the original process handler.

- **`order/create` and `order/fill` run once per tick**: After all symbol×timeframe goroutines complete (`wg.Wait()`), `runPipeline` calls `orderCreateHandler.Handle()` and then `orderFillHandler.Handle()` sequentially. They are not per-symbol/timeframe — they operate across all symbols in the DB. No second ticker is needed.

- **`order/fill` always uses 15-minute candles**: Regardless of the signal's original timeframe, fill checking fetches `15min` candles via `kucoin.Service.GetClosedCandles(ctx, symbol, "15min", 200)`. 200 candles covers ~50 hours of history. Only candles with `OpenTime >= order.CreatedAt` are checked.

- **Fill condition is price-cross**: An OPEN order is considered filled when any 15-minute candle since `CreatedAt` has `Low ≤ FillPrice` (BUY) or `High ≥ FillPrice` (SELL). Fill price is the order's `FillPrice` (= candidate `EntryPrice`). No slippage model in Week 3–4.

- **No order expiry**: OPEN orders remain OPEN indefinitely. Position count limits (`MaxOpenPositions`) in the `HardRiskGate` prevent stale orders from accumulating into a problematic state.

- **New SQL queries**: Five new queries are required:
  - `GetApprovedDecisionsWithoutOrder` (`:many`) — JOIN across `risk_decisions`, `candidate_trades`, LEFT JOIN `paper_orders`; returns rows where `decision = 'APPROVE' AND paper_orders.id IS NULL`. Returns decision ID, candidate ID, symbol, side, entry price.
  - `GetOpenPaperOrders` (`:many`) — selects all `paper_orders` where `status = 'OPEN'` ordered by `created_at ASC`.
  - `GetFilledPaperOrder` (`:one`) — fetches a single `paper_orders` row by ID where `status = 'FILLED'`; used by `order/close`.
  - `GetPaperFillByOrderId` (`:one`) — fetches the `paper_fills` row for a given `paper_order_id`; provides entry price and quantity for PnL calculation.
  - `ClosePosition` (`:exec`) — sets `quantity = 0` and `updated_at` for a given symbol; used by `order/close` after writing `TradeOutcome`.

- **`computeSnapshotInput` moves to `risk/evaluate`**: This pure function (derives portfolio state from `[]AccountBalance` and `[]Position`) previously lived in `candidatetrade/process`. It moves to `risk/evaluate/handler.go` with its test file.

- **`order/close` is HTTP API-triggered**: A minimal `net/http` server runs in a separate goroutine inside `cmd/main.go`. `POST /orders/{paper_order_id}/close` extracts the path param and calls `orderCloseHandler.Handle(ctx, Command{PaperOrderId})`. The server listens on `cfg.TradingBot.APIPort` (default `8080`) and shuts down gracefully when the main context is cancelled.

- **`order/close` exit price**: Fetched from KuCoin via `GetClosedCandles(ctx, symbol, "15min", 1)` — the close price of the most recent 15-minute candle. No additional API endpoints needed.

- **`TradeOutcome` written by `order/close`**: `InsertTradeOutcome` is called with `EntryPrice` (from `paper_fills.price`), `ExitPrice` (KuCoin current price), `Quantity`, `GrossPnL`, `Fees = 0`, `NetPnL = GrossPnL`, `ClosedAt = now`. After writing, `ClosePosition(symbol)` sets `positions.quantity = 0`. `TradingHistoryContext.DailyPnL` and `ConsecutiveLosses` become meaningful as soon as the first `TradeOutcome` is recorded.

- **KuCoin V2 Signature authentication**: `kucoin.Config` holds `APIKey`, `APISecret`, and `Passphrase`. `kucoin.NewService(cfg Config)` is the single constructor — pass a populated `Config` for authenticated use, empty `Config{}` for public-only use. Auth is encapsulated in `kucoin.Service.newAuthRequest()`.

- **Spot trading only**: `Position` tracks asset holdings derived from fills. No leverage, no margin, no liquidation price. `Exposure = FilledQuantity × CurrentPrice`.

- **Journal immutability contract**:
  - Immutable (INSERT only, never UPDATE): `portfolio_snapshots`, `risk_contexts`, `risk_decisions`, `paper_fills`
  - Mutable with `updated_at`: `paper_orders` (status transitions), `positions` (quantity and price updates)

- **`RiskContext` raw storage**: `risk_contexts` table stores the full assembled `RiskContext` as a JSON column. This is the primary input the LLM will receive in Week 7–8.

- **Config additions**: `cfg.KuCoin` typed as `kucoin.Config`. `cfg.RiskLimits` with defaults:
  ```
  MaxDailyLossUsdt:         300
  MaxPortfolioExposureUsdt: 5000
  MaxSymbolExposureUsdt:    2000
  MaxOpenPositions:         5
  MaxConsecutiveLosses:     3
  MinOrderSizeUsdt:         10
  ```

- **Database migrations**: New tables via `golang-migrate` numbered `000002_*` onward. Tables: `account_balances`, `portfolio_snapshots`, `positions`, `paper_orders`, `paper_fills`, `risk_contexts`, `risk_decisions`, `trade_outcomes`. All `id` columns are UUIDs. All `candidate_id` columns are foreign keys to `candidate_trades.id`.

---

## Testing Decisions

A good test verifies observable behavior at the highest testable seam. Tests must be fast, offline (no live KuCoin), and deterministic. Do not test internal computation directly when the public interface can exercise it.

**Seam 1 — `HardRiskGate.Evaluate(RiskContext) RiskDecision`**
- Pure function: no DB, no mocks needed.
- Required cases: each of the 6 reject rules triggered individually (daily loss, portfolio exposure, symbol exposure, open positions, consecutive losses, insufficient balance), plus the happy-path approve case where all limits pass.
- Verify `RiskDecision.Decision` and `RiskDecision.Reasons` for every case.
- Prior art: `internal/strategy/divergence/strategy_test.go` — same pattern: construct context struct, call method, assert output.

**Seam 2 — `risk/evaluate.handler.Handle(ctx, Command{TradeId})`**
- Use hand-written stub `generated.Queries` (backed by in-memory map) and a stub `RiskContextService`.
- Required cases: REJECT path (decision journaled, no order created); APPROVE path (decision journaled).
- Verify that journal writes receive the correct `candidate_id`.
- Prior art: standard Go interface mocking with hand-written stubs.

**Seam 3 — `order/fill.handler.Handle()` with stub KuCoin**
- Use a stub `kucoin.Service` (via `httptest.Server`) returning hardcoded 15-min candles.
- BUY fill case: candle with `Low ≤ EntryPrice` → order transitions to FILLED, fill row and position written.
- No-fill case: all candles have `Low > EntryPrice` → order remains OPEN.
- SELL fill case: candle with `High ≥ EntryPrice` → order transitions to FILLED.
- Prior art: `auth_bot` uses `httptest.Server` stubs and `testify` assertions.

**Seam 4 — `order/close.handler.Handle()` with stub KuCoin**
- Use a stub `kucoin.Service` that returns a hardcoded current price (latest 15-min candle close).
- BUY position closed at profit: `ExitPrice > EntryPrice` → `NetPnL > 0`, `trade_outcomes` row written, `positions.quantity = 0`.
- SELL position closed at profit: `ExitPrice < EntryPrice` → `NetPnL > 0`.
- Verify `InsertTradeOutcome` receives the correct `candidate_id` and `paper_fill_id`.
- Prior art: same `httptest.Server` stub pattern as Seam 3.

**Seam 5 — Full integration with real DB**
- Spin up a real PostgreSQL instance via `testcontainers`.
- Run the full sequence: insert a `CandidateTrade` → call `risk/evaluate` → call `order/create` → call `order/fill` (stub KuCoin returns a crossing candle) → call `order/close` (stub KuCoin returns exit price).
- Verify rows exist in `risk_decisions`, `paper_orders`, `paper_fills`, `positions`, `trade_outcomes` with matching `candidate_id`.
- Verify re-processing produces no additional rows (idempotency via `ON CONFLICT DO NOTHING`).
- Prior art: `auth` and `email_consumer` controller tests using `testcontainers`.

---

## Out of Scope

- WebSocket subscription to KuCoin for real-time balance/order/position updates
- Periodic REST reconciliation interval
- Real order submission to KuCoin
- LLM Risk Manager integration (Week 7–8)
- RAG / vector database (Week 5–6)
- Futures, margin, leverage, or liquidation logic
- Multi-exchange support
- Stop-loss and take-profit price calculation
- Order expiry for unfilled paper orders
- Automatic reconciliation correction on hard mismatch

---

## Vertical Slices

Each slice is independently deliverable, leaves the system in a working state, and has a clear done condition.

---

### Slice 1 — Database migrations and sqlc codegen

Deliverable: All new PostgreSQL tables exist and sqlc-generated query code compiles including all new queries.

New tables (`000002_*` onward): `account_balances`, `portfolio_snapshots`, `positions`, `paper_orders`, `paper_fills`, `risk_contexts`, `risk_decisions`, `trade_outcomes`.

Done when:
- All migrations run cleanly
- All five new queries are in the sqlc schema: `GetApprovedDecisionsWithoutOrder`, `GetOpenPaperOrders`, `GetFilledPaperOrder`, `GetPaperFillByOrderId`, `ClosePosition`
- `go build ./...` passes with zero errors
- Immutable tables have no UPDATE queries

---

### Slice 2 — Portfolio sync and KuCoin account methods

Deliverable: `portfolio/sync/handler.go` and two new KuCoin service methods provide portfolio sync on startup.

Done when:
- `kucoin.Service.GetBalances(ctx)` fetches spot account balances using V2 Signature auth
- `kucoin.Service.GetOpenOrders(ctx)` fetches active spot orders
- `portfolio/sync.handler.Handle()` deletes all account balances and upserts fresh ones from KuCoin
- Startup sync failure is non-fatal

---

### Slice 3 — HardRiskGate

Deliverable: `internal/risk/gate.go` enforces all 6 hard risk rules as a pure function.

Done when:
- All 6 REJECT conditions implemented with named reason constants
- APPROVE when all limits pass
- Seam 1 test suite passes (7 cases)

---

### Slice 4 — RiskContextService

Deliverable: `internal/risk/context.go` assembles a complete `RiskContext` and persists it.

Done when:
- `Build()` assembles `PortfolioSnapshot`, `PositionRiskContext`, `TradingHistoryContext`, `RiskLimits`
- Persists `portfolio_snapshot` and `risk_context` (immutable inserts)
- `RiskContext` JSON stored in `risk_contexts.context_json`

---

### Slice 5 — `risk/evaluate` handler

Deliverable: `application/risk/evaluate/handler.go` receives a `TradeId`, runs the full risk pipeline, and persists a `RiskDecision`.

Done when:
- Fetches candidate, computes portfolio snapshot via `computeSnapshotInput()` (moved from deleted `process/` package)
- Calls `riskSvc.Build()` and `gate.Evaluate()`
- Persists `RiskDecision` always (APPROVE and REJECT)
- Logs `risk_context_created`, `risk_decision_created`, `risk_decision_rejected`
- `candidatetrade/process/` package deleted; `computeSnapshotInput` + test moved to `risk/evaluate/`
- Seam 2 test suite passes

---

### Slice 6 — `order/create` handler

Deliverable: `application/order/create/handler.go` creates OPEN paper orders for every approved decision that lacks one.

Done when:
- Calls `GetApprovedDecisionsWithoutOrder` (JOIN query)
- Calls `InsertPaperOrder` with status `OPEN` for each result
- Logs `paper_order_created`
- Idempotent: re-running produces no duplicate orders (`ON CONFLICT DO NOTHING`)

---

### Slice 7 — `order/fill` handler

Deliverable: `application/order/fill/handler.go` checks OPEN orders against 15-minute market data and fills crossed orders.

Done when:
- Calls `GetOpenPaperOrders`
- Fetches 15-min candles via `kucoin.Service.GetClosedCandles(ctx, symbol, "15min", 200)`
- Filters candles to those with `OpenTime >= order.CreatedAt`
- Fill condition: `Low ≤ FillPrice` (BUY) or `High ≥ FillPrice` (SELL)
- On fill: `UpdatePaperOrderStatus` → `FILLED`, `InsertPaperFill`, `UpsertPosition`
- Logs `paper_order_filled`
- Seam 3 test suite passes

---

### Slice 8 — `order/close` handler and HTTP server

Deliverable: `application/order/close/handler.go` closes a FILLED paper order on demand, writes `TradeOutcome`, and zeroes the position. A minimal HTTP server exposes this via `POST /orders/{paper_order_id}/close`.

Done when:
- `GetFilledPaperOrder` and `GetPaperFillByOrderId` queries return the needed data
- Exit price fetched via `kucoin.Service.GetClosedCandles(ctx, symbol, "15min", 1)` → last candle `Close`
- `InsertTradeOutcome` written with correct PnL direction per side
- `ClosePosition(symbol)` sets `positions.quantity = 0`
- Logs `trade_outcome_created`
- HTTP server on `cfg.TradingBot.APIPort` (default `8080`) handles `POST /orders/{id}/close`
- Seam 4 test suite passes (BUY profit, SELL profit, correct FK linkage)

---

### Slice 9 — Binary wiring and end-to-end test

Deliverable: `cmd/main.go` wires all handlers, `runPipeline` calls them in the correct sequence, HTTP server runs alongside the ticker, and the full integration test passes.

Done when:
- `runPipeline` calls `find` → `risk/evaluate` (inline, per goroutine) → `order/create` → `order/fill` (sequential, after `wg.Wait()`)
- `processcandidatetrade.NewHandler` removed from wiring
- `riskevaluate.NewHandler`, `ordercreate.NewHandler`, `orderfill.NewHandler`, `orderclose.NewHandler` wired at startup
- HTTP server goroutine started; graceful shutdown tied to main context
- Seam 5 integration test passes
- End-to-end manual test: observe full log sequence `risk_context_created` → `risk_decision_created` → `paper_order_created` → `paper_order_filled` → (curl close) → `trade_outcome_created`; DB rows in all tables with matching `candidate_id`

---

## Further Notes

- The `order/fill` handler fetches 15-min candles on every tick for every OPEN order. With few OPEN orders this is fine. If orders accumulate, batching by symbol (one KuCoin call per unique symbol) becomes worthwhile — defer until needed.
- `TradingHistoryContext` statistics (`DailyPnL`, `ConsecutiveLosses`) become meaningful as soon as the first `TradeOutcome` row is written via `order/close`. The risk gate's daily-loss and consecutive-loss rules activate from that point forward.
- The double-gate architecture (HardRiskGate before and after LLM) is the permanent enforcement boundary. When the LLM is introduced in Week 7–8, it is inserted between the two gate passes — it does not replace either pass.
- When live execution is introduced, `order/fill` becomes `order/fill-real` — checking KuCoin fill status instead of simulating price-cross. The handler interface remains unchanged.
- `RiskContext` JSON stored in `risk_contexts` is the primary input the LLM will receive in Week 7–8. Storing it now ensures the schema is stable before LLM integration begins.
