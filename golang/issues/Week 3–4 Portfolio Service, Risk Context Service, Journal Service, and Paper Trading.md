Status: ready-for-agent

# Spec: Week 3–4 Portfolio Service, Risk Context Service, Journal Service, and Paper Trading

---

## Problem Statement

The trading system can generate `CandidateTrade` records, but nothing happens after they are persisted. There is no portfolio awareness (balance, positions, exposure), no risk evaluation before acting on a signal, no record of what happened to each candidate, and no protection against trading into an overexposed or loss-limited state. The system cannot answer: "Was this candidate safe to act on? What happened next? Why?" Without this infrastructure, the AI Risk Manager (Week 7–8) has nothing structured to evaluate and no history to reason over.

---

## Solution

Extend the `trading_bot` binary with two new internal packages (`risk`, `portfolio`) plus a new `processcandidatetrade` application handler. When a `CandidateTrade` is created by the Strategy Engine, `processcandidatetrade.handler` orchestrates: portfolio snapshot retrieval, risk context assembly, hard risk rule evaluation, and (on approval) paper trade execution with full journal persistence. All of this runs in paper trading mode — no real orders, no real money — producing a realistic lifecycle record that will feed the AI Risk Manager and RAG service in later weeks.

---

## Architecture

### Component Diagram

```plantuml
@startuml Week3-4 Component Architecture

skinparam componentStyle rectangle
skinparam packageStyle frame
skinparam linetype ortho

package "cmd/main.go" {
  [runPipeline]
}

package "application/candidatetrade" {
  [find.handler]
  [process.handler]
}

package "application/portfolio" {
  [sync.handler]
}

package "internal/risk" {
  [RiskContextService]
  [HardRiskGate] <<pure function>>
}

package "PostgreSQL" {
  database "candidate_trades" as CT
  database "account_balances" as AB
  database "portfolio_snapshots" as PS
  database "positions" as POS
  database "paper_orders" as PO
  database "paper_fills" as PF
  database "risk_contexts" as RC
  database "risk_decisions" as RD
  database "trade_outcomes" as TO
}

cloud "KuCoin REST API" {
  [GET /api/v1/accounts]
  [GET /api/v1/orders]
}

' Pipeline orchestration
[runPipeline] --> [find.handler] : Handle(ctx, Command{})
[find.handler] --> CT : InsertCandidateTrade (ON CONFLICT DO NOTHING)
[runPipeline] --> [process.handler] : Handle(ctx, Command{TradeId}) [only when TradeId != ""]

[process.handler] --> AB : GetAccountBalances (via generated.Queries)
[process.handler] --> POS : GetOpenPositions (via generated.Queries)
[process.handler] --> [RiskContextService] : Build()
[process.handler] --> [HardRiskGate] : Evaluate()
[process.handler] --> RD : InsertRiskDecision (via generated.Queries)
[process.handler] --> PO : InsertPaperOrder / UpdateStatus [APPROVE only]
[process.handler] --> PF : InsertPaperFill [APPROVE only]
[process.handler] --> POS : UpsertPosition [APPROVE only]

' Portfolio sync (startup)
[sync.handler] --> [kucoin.Service] : GetBalances() / GetOpenOrders() [startup]
[kucoin.Service] --> [GET /api/v1/accounts] : V2 Signature auth (kucoin.Config)
[kucoin.Service] --> [GET /api/v1/orders] : V2 Signature auth (kucoin.Config)
[sync.handler] --> AB : delete + upsert

' Risk context assembly
[RiskContextService] --> POS : read (symbol exposure)
[RiskContextService] --> TO : read (TradingHistoryContext)
[RiskContextService] --> RC : INSERT risk_context (via generated.Queries)
[RiskContextService] --> PS : INSERT portfolio_snapshot (via generated.Queries)

@enduml
```

### Pipeline Sequence Diagram

```plantuml
@startuml ProcessCandidateTrade Sequence

participant "runPipeline" as PP
participant "find.handler" as H
participant "process.handler" as CP
participant "RiskContextService" as RCS
participant "HardRiskGate\n<<pure function>>" as HRG
database "PostgreSQL\n(generated.Queries)" as DB

PP -> H : Handle(ctx, Command{})
H -> DB : InsertCandidateTrade (ON CONFLICT DO NOTHING)
H --> PP : Result{TradeId}
PP -> CP : Handle(ctx, Command{TradeId}) [only when TradeId != ""]

group Portfolio Snapshot
  CP -> DB : GetAccountBalances()
  CP -> DB : GetOpenPositions()
  CP -> CP : computeSnapshotInput() [pure function]
end

group Risk Context Assembly
  CP -> RCS : Build(ctx, candidate, snapshotInput)
  RCS -> DB : read positions (symbol exposure)
  RCS -> DB : read trade_outcomes (DailyPnL, ConsecutiveLosses)
  RCS -> DB : INSERT portfolio_snapshots
  RCS -> DB : INSERT risk_contexts
  RCS --> CP : RiskContext
  CP -> CP : log risk_context_created
end

group Hard Risk Gate
  CP -> HRG : Evaluate(riskContext)
  note right of HRG : No DB or network calls\nPure function
  HRG --> CP : RiskDecision (APPROVE / REJECT)
  CP -> DB : INSERT risk_decisions
  CP -> CP : log risk_decision_created
end

alt RiskDecision == APPROVE
  group Paper Trade Execution (process.handler.executePaperTrade)
    CP -> DB : INSERT paper_orders [OPEN]
    CP -> DB : UPDATE paper_orders [FILLED]
    CP -> DB : INSERT paper_fills
    CP -> DB : INSERT / UPDATE positions
    CP -> CP : log paper_order_filled
  end
else RiskDecision == REJECT
  CP -> CP : log risk_decision_rejected
  note right : executePaperTrade\nnot called
end

@enduml
```

### Journal Record Linkage (FK Chain)

```plantuml
@startuml Journal FK Chain

skinparam linetype ortho

entity "candidate_trades" as CT {
  * id : UUID <<PK>>
  --
  symbol
  entry_price
  indicators (JSON)
  created_at
}

entity "portfolio_snapshots" as PS {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  total_equity_usdt
  available_cash_usdt
  total_exposure_usdt
  open_position_count
  snapshot_at
}

entity "risk_contexts" as RC {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  portfolio_snapshot_id : UUID <<FK>>
  context_json (JSON)
  generated_at
}

entity "risk_decisions" as RD {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  risk_context_id : UUID <<FK>>
  decision : APPROVE|REJECT|REDUCE
  reasons (JSON)
  decided_at
}

entity "paper_orders" as PO {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  risk_decision_id : UUID <<FK>>
  symbol
  side
  quantity
  status : OPEN|FILLED
  updated_at
}

entity "paper_fills" as PF {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  paper_order_id : UUID <<FK>>
  quantity
  price
  executed_at
}

entity "positions" as POS {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  symbol
  filled_quantity
  avg_entry_price
  current_exposure_usdt
  updated_at
}

entity "trade_outcomes" as TO {
  * id : UUID <<PK>>
  --
  candidate_id : UUID <<FK>>
  paper_fill_id : UUID <<FK>>
  entry_price
  exit_price
  quantity
  gross_pnl
  fees
  net_pnl
  closed_at
}

CT ||--o{ PS : "candidate_id"
CT ||--o{ RC : "candidate_id"
CT ||--o{ RD : "candidate_id"
CT ||--o{ PO : "candidate_id"
CT ||--o{ PF : "candidate_id"
CT ||--o{ POS : "candidate_id"
CT ||--o{ TO : "candidate_id"

PS ||--o{ RC : "portfolio_snapshot_id"
RC ||--o{ RD : "risk_context_id"
RD ||--o{ PO : "risk_decision_id"
PO ||--o{ PF : "paper_order_id"
PF ||--o{ TO : "paper_fill_id"

@enduml
```

### Full Component Relationship — Entry Point Overview

The diagram below shows every component, its dependencies, and the two trigger points that start the flow: the **startup sync** (runs once on binary launch) and the **poll ticker** (runs every `PollIntervalSeconds`).

```plantuml
@startuml Week3-4 Full Component Relationships

skinparam componentStyle rectangle
skinparam packageStyle frame
skinparam linetype ortho
skinparam defaultFontSize 11
skinparam ArrowColor #555555

' ─────────────────────────────────────────
' ENTRY POINTS
' ─────────────────────────────────────────
actor "OS / Process Start" as OS <<entry point>>
actor "time.Ticker\n(PollIntervalSeconds)" as TICK <<entry point>>

' ─────────────────────────────────────────
' cmd/main.go
' ─────────────────────────────────────────
package "cmd/main.go" {
  [startup wiring] <<once>>
  [runPipeline\n(goroutine per symbol×timeframe)] <<loop>>
}

' ─────────────────────────────────────────
' Application handlers
' ─────────────────────────────────────────
package "application/portfolio" {
  [sync.handler]
}

package "application/candidatetrade" {
  [find.handler]
  [process.handler]
}

' ─────────────────────────────────────────
' Domain / service packages
' ─────────────────────────────────────────
package "internal/services/kucoin" {
  [kucoin.Service\n(config, balance, open_order, service)]
}

package "internal/indicator" {
  [indicator.Engine]
}

package "internal/strategy/divergence" {
  [strategy.Strategy]
}

package "internal/risk" {
  [RiskContextService]
  [HardRiskGate\n<<pure function>>]
}

' ─────────────────────────────────────────
' Data stores
' ─────────────────────────────────────────
database "PostgreSQL" {
  [account_balances]
  [candidate_trades]
  [portfolio_snapshots]
  [risk_contexts]
  [risk_decisions]
  [paper_orders]
  [paper_fills]
  [positions]
  [trade_outcomes]
}

cloud "KuCoin REST API" {
  [/api/v1/accounts\n(balances)]
  [/api/v1/orders\n(open orders)]
  [/api/v3/market/candles\n(closed candles)]
}

' ─────────────────────────────────────────
' TRIGGER FLOWS
' ─────────────────────────────────────────
OS --> [startup wiring] : binary start
[startup wiring] --> [sync.handler] : Handle(Command{}) — non-fatal

TICK --> [runPipeline\n(goroutine per symbol×timeframe)] : every tick\n+ once on startup

' ─────────────────────────────────────────
' PIPELINE: find leg
' ─────────────────────────────────────────
[runPipeline\n(goroutine per symbol×timeframe)] --> [find.handler] : Handle(Command{Symbol, Timeframe, CandleLimit})
[find.handler] --> [kucoin.Service\n(config, balance, open_order, service)] : GetClosedCandles()
[kucoin.Service\n(config, balance, open_order, service)] --> [/api/v3/market/candles\n(closed candles)]
[find.handler] --> [indicator.Engine] : Calculate([]Candle)
[find.handler] --> [strategy.Strategy] : Evaluate(MarketContext)
[find.handler] --> [candidate_trades] : InsertCandidateTrade\n(ON CONFLICT DO NOTHING)
[find.handler] --> [runPipeline\n(goroutine per symbol×timeframe)] : Result{TradeId}

' ─────────────────────────────────────────
' PIPELINE: process leg (TradeId != "" only)
' ─────────────────────────────────────────
[runPipeline\n(goroutine per symbol×timeframe)] --> [process.handler] : Handle(Command{TradeId})\n[only when TradeId != ""]

[process.handler] --> [account_balances] : GetAccountBalances (via generated.Queries)
[process.handler] --> [positions] : GetOpenPositions (via generated.Queries)
[process.handler] --> [process.handler] : computeSnapshotInput() [pure]

[process.handler] --> [RiskContextService] : Build(ctx, candidate, snapshotInput)
[RiskContextService] --> [positions] : read (symbol exposure)
[RiskContextService] --> [trade_outcomes] : read (DailyPnL, ConsecutiveLosses)
[RiskContextService] --> [portfolio_snapshots] : INSERT (immutable)
[RiskContextService] --> [risk_contexts] : INSERT (immutable)

[process.handler] --> [HardRiskGate\n<<pure function>>] : Evaluate(RiskContext)\n[no DB / no network]
[process.handler] --> [risk_decisions] : INSERT (immutable)\n[always — APPROVE and REJECT]
[process.handler] --> [paper_orders] : INSERT / UPDATE\n[APPROVE only — executePaperTrade]
[process.handler] --> [paper_fills] : INSERT (immutable)\n[APPROVE only]
[process.handler] --> [positions] : INSERT / UPDATE\n[APPROVE only]

' ─────────────────────────────────────────
' STARTUP: portfolio sync leg
' ─────────────────────────────────────────
[sync.handler] --> [kucoin.Service\n(config, balance, open_order, service)] : GetBalances()\nGetOpenOrders()
[kucoin.Service\n(config, balance, open_order, service)] --> [/api/v1/accounts\n(balances)] : V2 Signature auth
[kucoin.Service\n(config, balance, open_order, service)] --> [/api/v1/orders\n(open orders)] : V2 Signature auth
[sync.handler] --> [account_balances] : DELETE + upsert

@enduml
```

---

## User Stories

1. As a developer, I want a `getportfoliosnapshot` handler that returns account balances, open positions, and a computed portfolio snapshot from the DB, so that risk context assembly has a single, testable source for portfolio state.
2. As a developer, I want a `syncportfolio` handler that calls `kucoin.Service.GetBalances()` on application startup and writes fresh balances to the DB, so that the internal DB reflects the real account state before the first pipeline run.
3. As a developer, I want `kucoin.Service.GetBalances()` and `kucoin.Service.GetOpenOrders()` to use KuCoin V2 Signature authentication via credentials stored in `kucoin.Config`, so that authenticated exchange calls are centralised in the kucoin service alongside the existing `GetClosedCandles`.
4. As a developer, I want internal portfolio state stored in PostgreSQL tables (`account_balances`, `orders`, `portfolio_snapshots`), so that state survives restarts and is available for risk context assembly without hitting KuCoin on every signal.
5. As a developer, I want KuCoin to be the source of truth — on any reconciliation conflict, exchange data wins — so that internal state never diverges silently from reality.
6. As a developer, I want a `PortfolioSnapshot` captured at the moment a `CandidateTrade` is evaluated, so that the exact portfolio state used in every risk decision is preserved immutably for audit and RAG.
7. As a developer, I want exposure calculated as `Quantity × Price` per asset and summed to total exposure, so that the risk gate can compare candidate exposure against portfolio-level limits.
8. As a developer, I want a `RiskContext` struct that aggregates `CandidateTrade`, `PortfolioSnapshot`, `PositionRiskContext`, `TradingHistoryContext`, `RiskLimits`, and `MarketRiskContext` into a single serializable envelope, so that the future LLM Risk Manager receives all relevant facts without querying any database or service directly.
9. As a developer, I want a `RiskContextService` that assembles `RiskContext` from portfolio and journal data, so that context assembly is a single testable operation with no side effects.
10. As a developer, I want `TradingHistoryContext` to include recent win/loss count, daily PnL, and consecutive losses derived from journal data, so that the risk gate and future LLM can factor in recent strategy performance.
11. As a developer, I want `RiskLimits` to be fully config-driven (max daily loss, max portfolio exposure, max symbol exposure, max open positions, max consecutive losses), so that risk thresholds can be changed without code changes.
12. As a developer, I want a `HardRiskGate` that evaluates a `RiskContext` against `RiskLimits` and returns a `RiskDecision` (APPROVE / REJECT / REDUCE), so that every candidate is evaluated against deterministic hard limits before any AI involvement.
13. As a developer, I want the `HardRiskGate` to reject on daily loss limit exceeded, so that the bot stops trading after a configured drawdown threshold.
14. As a developer, I want the `HardRiskGate` to reject on total portfolio exposure limit exceeded, so that the bot never over-allocates capital across all open positions.
15. As a developer, I want the `HardRiskGate` to reject on symbol exposure limit exceeded, so that the bot never concentrates too much capital in a single asset.
16. As a developer, I want the `HardRiskGate` to reject when open positions are at the configured maximum, so that position count is bounded.
17. As a developer, I want the `HardRiskGate` to reject on consecutive losses exceeding the configured threshold, so that the bot pauses after a losing streak.
18. As a developer, I want the `HardRiskGate` to reject on insufficient available balance, so that orders are never attempted without enough capital.
19. As a developer, I want the `HardRiskGate` to be a pure function with no database or network calls, so that it is fast, fully unit-testable, and deterministic.
20. As a developer, I want the `HardRiskGate` to run both before and after the LLM Risk Manager (double-gate), so that the LLM cannot override hard limits even if it returns APPROVE on a disqualified candidate.
21. As a developer, I want a `RiskDecision` record persisted to the journal as an immutable insert immediately after the `HardRiskGate` evaluates, so that every decision — including REJECTs — is traceable.
22. As a developer, I want `RiskDecision.Reasons` to contain named string constants (e.g. `DAILY_LOSS_LIMIT_EXCEEDED`, `INSUFFICIENT_BALANCE`), so that rejection reasons are human-readable and machine-parseable.
23. As a developer, I want paper trade execution (`executePaperTrade` — a private method on `process.handler`) to generate a synthetic `OrderFilled` at the candidate's entry price when a candidate is APPROVED, so that the full journal lifecycle can be tested and exercised without placing real orders on KuCoin.
24. As a developer, I want the paper trading engine to write a synthetic order, fill, and portfolio position update to the journal tables, so that trade history statistics (win rate, daily PnL, consecutive losses) are populated for subsequent risk context assembly.
25. As a developer, I want `CandidateJournal` records to be immutable inserts — never updated after creation — so that the exact indicator snapshot and market context that produced the candidate is preserved for audit and RAG.
26. As a developer, I want `RiskContext` records to be immutable inserts, so that the exact portfolio state at decision time is preserved and cannot be overwritten by later portfolio changes.
27. As a developer, I want `RiskDecision` records to be immutable inserts, so that the AI cannot retroactively alter what decision was made.
28. As a developer, I want `Order` and `Position` records to be mutable with `updated_at` tracking, so that operational state transitions (PENDING → OPEN → FILLED) are reflected correctly.
29. As a developer, I want a `TradeOutcome` record written when a paper position is closed, containing entry price, exit price, quantity, gross PnL, fees, and net PnL, so that strategy performance statistics can be computed from the journal.
30. As a developer, I want all journal records linked by a `candidate_id` foreign key chain (`candidate → risk_context → risk_decision → order → fill → trade_outcome`), so that the complete lifecycle of any trade is reconstructable from a single candidate ID.
31. As a developer, I want all journal tables to use `ON CONFLICT DO NOTHING` on their natural business key, so that idempotent re-processing never creates duplicate records.
32. As a developer, I want a `processcandidatetrade` handler (`internal/application/candidatetrade/process/handler.go`) that orchestrates portfolio snapshot → risk context → hard gate → paper execution → journal, so that `runPipeline` stays thin and the pipeline is independently testable.
33. As a developer, I want `runPipeline` to call `processHandler.Handle(ctx, Command{TradeId})` after `findHandler.Handle()` returns a non-empty `TradeId`, so that pipeline failure is non-fatal and does not prevent the candidate from being persisted.
34. As a developer, I want structured `slog` log events emitted at each pipeline stage (risk_context_created, risk_decision_created, paper_order_filled, trade_outcome_created) with `candidate_id` in every event, so that the full lifecycle is traceable in logs.
35. As a developer, I want the portfolio sync to be non-fatal on startup failure — proceeding with an empty portfolio snapshot — so that the bot continues to paper-trade even if KuCoin is temporarily unreachable.
36. As a developer, I want spot trading only in Week 3–4 (no leverage, no margin, no futures), so that the `Position` model stays simple and the risk calculations have no liquidation complexity.
37. As a developer, I want all new packages (`portfolio`, `risk`) to be internal to the `trading_bot` module, with all DB writes going through `generated.Queries` directly (no intermediate repository or engine wrapper), so that data ownership is explicit and the system remains a single deployable binary.

---

## Implementation Decisions

- **One binary, application handler pattern**: All business operations invoked directly from `runPipeline` follow the `app.Handler[Command, Result]` pattern in `internal/application/{feature}/{operation}/handler.go`. All KuCoin HTTP goes through `internal/services/kucoin/` (package `kucoin`). No new binaries, no gRPC, no HTTP between components.

- **Non-handler domain packages**: `internal/risk/` (`HardRiskGate`, `RiskContextService`) are the only domain packages injected as dependencies of `process.handler`. There is no `JournalRepository`, no `PaperTradingEngine`, and no `getsnapshot.handler` — all three are eliminated as unnecessary layers. Paper trade execution is a private method (`executePaperTrade`) on `process.handler`, portfolio snapshot computation is a private function (`computeSnapshotInput`) in the same package, and all DB writes go directly through `generated.Queries`.

- **KuCoin is source of truth**: The internal DB is a synchronized mirror. On startup, `syncportfolio.handler` populates `account_balances` from KuCoin. Phase 2 (WebSocket) and Phase 3 (REST interval) are out of scope for Week 3–4.

- **KuCoin V2 Signature authentication**: `kucoin.Config` holds `APIKey`, `APISecret`, and `Passphrase`. `kucoin.NewService(cfg Config)` is the single constructor — pass a populated `Config` for authenticated use, empty `Config{}` for public-only use. The service adds `KC-API-KEY`, `KC-API-SIGN` (HMAC-SHA256 of `timestamp + method + path + body`), `KC-API-TIMESTAMP`, `KC-API-PASSPHRASE` (HMAC-SHA256 of passphrase), and `KC-API-KEY-VERSION=2` headers on authenticated requests. Auth is encapsulated in `kucoin.Service.newAuthRequest()`. In `config.go`, `AppConfig.KuCoin` is typed as `kucoin.Config` directly — no duplication of credential fields.

- **Spot trading only**: `Position` tracks asset holdings derived from fills. No leverage, no margin, no liquidation price. `Exposure = FilledQuantity × CurrentPrice`.

- **`HardRiskGate` is a pure function**: `func (g *HardRiskGate) Evaluate(ctx RiskContext) RiskDecision`. It reads `RiskContext` and `RiskLimits` only. No DB calls, no network calls. Runs before the (future) LLM call and again after — the second pass ensures the LLM cannot produce an APPROVE that violates limits.

- **`RiskDecision` type**:
  ```
  APPROVE — all hard limits pass; proceed to paper execution
  REJECT  — one or more hard limits violated; record reason, skip execution
  REDUCE  — reserved for future use (position sizing reduction); treated as APPROVE in Week 3–4
  ```

- **Paper trading synthetic fill**: Entry price = `CandidateTrade.EntryPrice` (= candle close price at signal time). No slippage model in Week 3–4. Fill is written immediately with no simulated latency.

- **Journal immutability contract**:
  - Immutable (INSERT only, never UPDATE): `candidate_journal`, `risk_contexts`, `risk_decisions`, `paper_fills`, `trade_outcomes`
  - Mutable with `updated_at`: `paper_orders` (status transitions), `positions` (quantity and PnL updates)

- **`TradingHistoryContext` data source**: Derived from `trade_outcomes` table in the journal. `DailyPnL` = sum of `net_pnl` for `closed_at >= today 00:00 UTC`. `ConsecutiveLosses` = count of trailing losses from most recent completed trades.

- **`RiskContext` raw storage**: `risk_contexts` table stores the full assembled `RiskContext` as a JSON column alongside the structured foreign key to `candidate_id` and `portfolio_snapshot_id`. This enables the future LLM to receive a pre-serialized context without re-assembling it.

- **`processcandidatetrade` handler wiring in `cmd/main.go`**: `processcandidatetrade.handler` is instantiated once at startup alongside `find.handler` and passed into `runPipeline`. After `findHandler.Handle()` returns a non-empty `Result.TradeId`, `runPipeline` calls `processHandler.Handle(ctx, processcandidatetrade.Command{TradeId: result.TradeId})`. `find.handler.Handle()` is not modified — it continues to own candle fetching, indicator calculation, strategy evaluation, deduplication, and DB persistence. Process handler failure is logged but does not halt the process.

- **Config additions**: `cfg.KuCoin` is typed as `kucoin.Config` (API key, secret, passphrase — base URL is hardcoded in the kucoin package). `cfg.RiskLimits` (all six thresholds) is also added to the existing JSON config struct. Defaults:
  ```
  MaxDailyLossUsdt:        300
  MaxPortfolioExposureUsdt: 5000
  MaxSymbolExposureUsdt:   2000
  MaxOpenPositions:        5
  MaxConsecutiveLosses:    3
  MinOrderSizeUsdt:        10
  ```

- **Database migrations**: Eight new tables added via `golang-migrate`, following the same embed + `iofs` pattern as existing `candidate_trades`. Migration files are numbered `000002_*` onward — `000001_create_candidate_trades` is already applied. All `id` columns are UUIDs. All `candidate_id` columns are foreign keys to `candidate_trades.id`.

- **sqlc codegen**: All new SQL queries use sqlc-generated code, consistent with existing patterns in `trading_bot/internal/database/generated/`.

---

## Testing Decisions

A good test verifies observable behavior at the highest testable seam. Tests must be fast, offline (no live KuCoin), and deterministic. Do not test internal computation directly when the public interface can exercise it.

**Seam 1 — `HardRiskGate.Evaluate(RiskContext) RiskDecision`**
- Pure function: no DB, no mocks needed.
- Required cases: each of the 6 reject rules triggered individually (daily loss, portfolio exposure, symbol exposure, open positions, consecutive losses, insufficient balance), plus the happy-path approve case where all limits pass.
- Verify `RiskDecision.Decision` and `RiskDecision.Reasons` for every case.
- Prior art: `internal/strategy/divergence/strategy_test.go` (same pattern — construct context struct, call method, assert output).

**Seam 2 — `processcandidatetrade.handler.Handle(ctx, Command{TradeId})`**
- Use a stub `generated.Queries` (hand-written, backed by an in-memory map) for DB calls, and a stub `RiskContextService` (hand-written). No `PaperTradingEngine`, `getsnapshot.handler`, or `JournalRepository` — all eliminated.
- Required cases: REJECT path (hard gate fires, paper engine not called, rejection journaled); APPROVE path (gate passes, paper engine called, lifecycle journaled).
- Verify that the paper engine is not called when the gate rejects.
- Verify that all journal writes receive the correct `candidate_id`.
- Prior art: no direct prior art in this repo; use standard Go interface mocking with hand-written stubs.

**Seam 3 — Full integration with real DB**
- Spin up a real PostgreSQL instance (testcontainers, matching the pattern in `auth` and `email_consumer` controller tests).
- Use a stub `kucoin.Service` (via `httptest.Server`) that returns hardcoded balances and no open orders.
- Run the full `processcandidatetrade.handler.Handle()` with a constructed `CandidateTrade` ID.
- Verify that rows exist in `risk_contexts`, `risk_decisions`, `paper_orders`, `paper_fills` tables.
- Verify that all rows share the same `candidate_id`.
- Verify that re-processing the same `CandidateTrade` produces exactly the same number of rows (idempotency).

---

## Out of Scope

- WebSocket subscription to KuCoin for real-time balance/order/position updates (Phase 2 — future week)
- Periodic REST reconciliation interval (Phase 3 — future week)
- Real order submission to KuCoin (requires execution service — future week)
- LLM Risk Manager integration (Week 7–8)
- RAG / vector database (Week 5–6)
- Futures, margin, leverage, or liquidation logic
- Multi-exchange support
- Stop-loss and take-profit price calculation (no execution layer yet)
- Automatic reconciliation correction — on hard mismatch, the system logs and alerts; no auto-correction

---

## Vertical Slices

Each slice is independently deliverable, has a clear done condition, and leaves the system in a working state. Slices are ordered by dependency — each one builds on the previous.

---

### Slice 1 — Database migrations and sqlc codegen

Deliverable: All new PostgreSQL tables exist and sqlc-generated query code compiles.

New tables (migration files `000002_*` onward): `account_balances`, `portfolio_snapshots`, `positions`, `paper_orders`, `paper_fills`, `risk_contexts`, `risk_decisions`, `trade_outcomes`.

Done when:
- All migrations run cleanly against a local PostgreSQL instance (starting from `000002_*`)
- sqlc generates typed query functions for every new table
- `go build ./...` passes with zero errors
- All `id` columns are UUIDs; all `candidate_id` columns are foreign keys to `candidate_trades.id`
- Immutable tables have no UPDATE queries in sqlc schema

No business logic. No KuCoin calls. No structs beyond what sqlc generates.

---

### Slice 2 — Portfolio handlers and KuCoin account methods

Deliverable: One application handler and two new KuCoin service methods provide portfolio sync on startup.

New files:
- `internal/application/portfolio/sync/handler.go` — `SyncFromExchange` handler
- `internal/services/kucoin/` package — split into `config.go` (`Config`), `balance.go` (`Balance`), `open_order.go` (`OpenOrder`), `service.go` (`Service`, `NewService(cfg Config)`, `GetClosedCandles`, `GetBalances`, `GetOpenOrders`)

There is no `getsnapshot` handler. Portfolio state is read directly in `process.handler` via `generated.Queries` and computed with the `computeSnapshotInput()` pure function (in `process/snapshot_test.go`).

Done when:
- `kucoin.Service.GetBalances(ctx)` fetches spot account balances from KuCoin using V2 Signature authentication
- `kucoin.Service.GetOpenOrders(ctx)` fetches active spot orders from KuCoin
- `syncportfolio.handler.Handle(ctx, Command{})` deletes all account balances and upserts fresh ones from KuCoin; open orders are fetched for observability but not persisted separately
- Startup sync failure is non-fatal: `cmd/main.go` calls sync handler, logs error, continues
- `kucoin.Balance` and `kucoin.OpenOrder` types are defined in the kucoin package

No risk logic. No journal writes. No pipeline.

---

### Slice 3 — Journal SQL queries and sqlc codegen

Deliverable: All journal SQL queries exist and sqlc generates typed functions for every insert/update operation.

No `JournalRepository` wrapper. `RiskContextService` and `process.handler` call `generated.Queries` directly — the sqlc-generated code is the data-access layer.

Done when:
- SQL files define INSERT-only queries for `risk_contexts`, `risk_decisions`, `paper_fills`, `trade_outcomes` (no UPDATE queries on these types)
- SQL files define INSERT + UPDATE queries for `paper_orders` and `positions`
- All INSERT queries use `ON CONFLICT DO NOTHING` on their natural business key
- `candidate_id` is present on every table and correctly threaded through query params
- `generated.Queries` has typed methods: `InsertRiskContext`, `InsertRiskDecision`, `InsertPaperOrder`, `UpdatePaperOrderStatus`, `InsertPaperFill`, `UpsertPosition`, `InsertTradeOutcome`, `GetDailyPnL`, `GetRecentTradeOutcomes`
- `go build ./...` passes with zero errors

No business logic. No pipeline. No KuCoin calls.

---

### Slice 4 — HardRiskGate

Deliverable: `HardRiskGate.Evaluate(RiskContext) RiskDecision` enforces all six hard risk rules as a pure function.

New file: `internal/risk/gate.go` (package `risk`) — same pattern as `internal/strategy/divergence/`: pure domain function, no DB, no network. Not a handler.

Done when:
- REJECT on `DailyPnL <= -MaxDailyLoss` with reason `DAILY_LOSS_LIMIT_EXCEEDED`
- REJECT on `TotalExposure + candidateExposure > MaxPortfolioExposure` with reason `PORTFOLIO_EXPOSURE_EXCEEDED`
- REJECT on `SymbolExposure + candidateExposure > MaxSymbolExposure` with reason `SYMBOL_EXPOSURE_EXCEEDED`
- REJECT on `OpenPositions >= MaxOpenPositions` with reason `MAX_OPEN_POSITIONS_REACHED`
- REJECT on `ConsecutiveLosses >= MaxConsecutiveLosses` with reason `CONSECUTIVE_LOSSES_EXCEEDED`
- REJECT on `AvailableBalance < MinOrderSizeUsdt` with reason `INSUFFICIENT_BALANCE`
- APPROVE when all limits pass
- No DB calls, no network calls in `Evaluate()`
- Seam 1 test suite passes (7 cases: 6 reject + 1 approve)

No pipeline. No journal writes.

---

### Slice 5 — RiskContextService

Deliverable: `RiskContextService.Build(ctx, candidate)` assembles a complete `RiskContext` from portfolio and journal data and persists it as an immutable record.

New file: `internal/risk/context.go` (package `risk`) — service struct with DB side effects (calls `generated.Queries.InsertRiskContext()` directly). Not a handler; injected into the `process` handler.

Done when:
- `Build()` retrieves `PortfolioSnapshot`, derives `PositionRiskContext` (existing exposure per symbol, open position count), and derives `TradingHistoryContext` (daily PnL, consecutive losses, win rate) from `trade_outcomes` in the journal
- `MarketRiskContext` is populated from `CandidateTrade.Indicators`
- `RiskLimits` populated from config
- Assembled `RiskContext` is persisted to `risk_contexts` table as an immutable insert with its JSON representation
- `RiskContext.GeneratedAt` is set at assembly time

---

### Slice 6 — Paper trade execution (inlined into process handler)

Deliverable: `process.handler.executePaperTrade` generates a synthetic fill and writes the complete paper trade lifecycle directly via `generated.Queries`.

No separate package. `executePaperTrade` is a private method on `process.handler` — it is only ever called from one place and belongs there.

Done when:
- A `paper_orders` row is created with status `OPEN`, then immediately updated to `FILLED`
- A `paper_fills` row is created with `Quantity`, `Price = candidate.EntryPrice`, and `ExecutedAt = now`
- A `positions` row is created or updated to reflect the new holding
- No real KuCoin order is placed
- All written records carry the same `candidate_id`
- Structured `slog` event `paper_order_filled` is emitted with `candidate_id`, `symbol`, `side`, `fill_price`

---

### Slice 7 — Process handler and binary wiring

Deliverable: `processcandidatetrade.handler` orchestrates all prior slices end-to-end, and `runPipeline` in `cmd/main.go` calls it after persisting the candidate.

New file: `internal/application/candidatetrade/process/handler.go` (package `processcandidatetrade`) — follows the standard handler pattern:
```go
type Command struct { TradeId string }
type Result struct { Decision string; Approved bool }
type handler struct {
    queries *generated.Queries
    riskSvc *risk.RiskContextService
    gate    *risk.HardRiskGate
    limits  risk.RiskLimits
    logger  *slog.Logger
}
func NewHandler(...) app.Handler[Command, Result]
// private helpers: executePaperTrade, computeSnapshotInput, baseAsset
```

Done when:
- `processcandidatetrade.handler.Handle(ctx, Command{TradeId})` runs: portfolio snapshot → risk context → hard gate → (if APPROVE) paper execution
- REJECT path: `RiskDecision` is journaled, `executePaperTrade` is not called, structured log `risk_decision_rejected` emitted
- APPROVE path: `RiskDecision` journaled, `executePaperTrade` called, full lifecycle written
- `runPipeline` calls `processHandler.Handle(ctx, processcandidatetrade.Command{TradeId: result.TradeId})` when `result.TradeId != ""`; `find.handler.Handle()` is not modified; process handler error is logged but non-fatal
- `cmd/main.go` wires at startup: `kucoin.NewService(cfg.KuCoin)`, `syncportfolio.NewHandler`, `risk.NewRiskContextService(queries, cfg.RiskLimits)`, `risk.NewHardRiskGate()`, `processcandidatetrade.NewHandler(queries, riskSvc, gate, cfg.RiskLimits, logger)`
- KuCoin credentials and risk limits loaded from JSON config
- End-to-end manual test: start the binary, observe `risk_context_created`, `risk_decision_created`, and `paper_order_filled` log events and matching DB rows

---

### Slice 8 — Test suites

Deliverable: All three test seams are covered and passing.

Done when:
- **Seam 1**: `HardRiskGate` unit tests pass for all 7 cases (6 reject + 1 approve) — no DB, no mocks
- **Seam 2**: `processcandidatetrade.handler` tests with hand-written stubs pass for REJECT path (`executePaperTrade` not called) and APPROVE path (`executePaperTrade` called, all DB writes carry the correct `candidate_id`)
- **Seam 3**: Integration test with testcontainers PostgreSQL passes — rows exist in `risk_contexts`, `risk_decisions`, `paper_orders`, `paper_fills` after `Process()`; all rows share the same `candidate_id`; re-processing the same candidate produces no additional rows (idempotency)

---

## Further Notes

- When live execution is introduced in a future week, `executePaperTrade` becomes `executeRealTrade` — a method swap inside `process.handler`. No changes needed to `runPipeline`, `RiskContextService`, or `HardRiskGate`.
- The double-gate architecture (HardRiskGate before and after LLM) is the permanent enforcement boundary. When the LLM is introduced in Week 7–8, it is inserted between the two gate passes — it does not replace either pass.
- `TradingHistoryContext` statistics derived from `trade_outcomes` will initially reflect paper trades only. This is intentional: the statistics accumulate over time and will be meaningful after several days of paper trading.
- The `RiskContext` JSON column in `risk_contexts` is the primary input the LLM will receive in Week 7–8. Storing it now ensures the schema is stable before the LLM integration begins.
- Phase 2 (WebSocket) should be implemented before switching from paper trading to live execution, since polling alone is too slow for accurate fill detection in a live system.
