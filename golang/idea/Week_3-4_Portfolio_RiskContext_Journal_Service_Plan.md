# Week 3–4 Implementation Plan
## Portfolio Service, Risk Context Service, and Journal Service

### Objective

Build the data and risk-context foundation required before introducing the LLM Risk Manager.

By the end of Week 4, the system should be able to answer:

- What is the current portfolio state?
- What positions and orders are open?
- How much exposure already exists?
- What risk limits apply?
- What happened in recent trades?
- What market and strategy context existed when a Candidate Trade was created?
- Can the complete lifecycle of a trade be reconstructed later?

Target flow:

```text
Candidate Trade
      │
      ├───────────────┐
      ▼               ▼
Portfolio Service   Journal Service
      │               │
      └───────┬───────┘
              ▼
      Risk Context Service
              │
              ▼
         Risk Context
              │
              ▼
     Future: LLM Risk Manager
```

---

# 1. Scope

## In Scope

### Portfolio Service
- Account balance and available balance
- Positions
- Open orders
- Exposure
- Unrealized and realized PnL
- Position lifecycle
- Portfolio snapshots
- Basic reconciliation
- Idempotent state updates

### Risk Context Service
- Candidate Trade context
- Portfolio context
- Existing exposure
- Symbol concentration
- Recent trading history
- Daily PnL
- Consecutive losses
- Strategy statistics
- Risk limits
- Market context
- RiskContext contract

### Journal Service
- Candidate Trade journal
- Risk decision journal
- Order journal
- Fill/execution journal
- Trade lifecycle
- Trade outcome and PnL
- Strategy version
- Market/portfolio snapshot
- Event and correlation IDs
- Historical reconstruction

### Reliability
- Idempotency
- Retry-safe processing
- Transaction boundaries
- Event correlation
- Data consistency
- Failure handling

---

# 2. Out of Scope

Do not implement these during Week 3–4:

- LLM Risk Manager
- RAG/vector database
- ML prediction
- Real-money automated execution
- Advanced portfolio optimization
- Dynamic LLM position sizing
- Complex multi-exchange portfolio management

The goal is to build reliable data and risk infrastructure first.

---

# 3. Architecture Principles

## Separation of Responsibilities

```text
Portfolio Service
→ What do we currently own?

Risk Context Service
→ What facts are relevant to this candidate?

Risk Engine
→ Is the trade allowed?

LLM Risk Manager
→ Future contextual risk assessment

Execution Service
→ How is the approved trade executed?

Journal Service
→ What happened and why?
```

## Important Rule

The LLM should receive a structured `RiskContext`. It should not directly query PostgreSQL, Redis, exchange APIs, or internal services.

---

# Week 3 — Portfolio Service

# 4. Portfolio Service Responsibilities

The Portfolio Service maintains the current portfolio state.

It should provide:

```text
Account balance
Available balance
Locked balance
Open positions
Open orders
Unrealized PnL
Realized PnL
Total exposure
Exposure by symbol
Exposure by side
```

It should not decide whether a Candidate Trade is good or bad.

---

# 5. Define Account Balance

```go
type AccountBalance struct {
    AccountID string

    Asset string

    Total     decimal.Decimal
    Available decimal.Decimal
    Locked    decimal.Decimal

    UpdatedAt time.Time
}
```

Example:

```text
USDT
Total     = 10,000
Available = 8,000
Locked    = 2,000
```

---

# 6. Define Position

```go
type Position struct {
    ID string

    AccountID string
    Symbol    string

    Side Side

    Quantity     decimal.Decimal
    EntryPrice   decimal.Decimal
    CurrentPrice decimal.Decimal

    UnrealizedPnL decimal.Decimal
    RealizedPnL   decimal.Decimal

    NotionalValue decimal.Decimal

    OpenedAt  time.Time
    UpdatedAt time.Time
}
```

For the first implementation, supporting LONG positions is sufficient if the strategy is long-only.

---

# 7. Define Order

```go
type Order struct {
    ID string

    ClientOrderID string
    ExchangeOrderID string

    AccountID string
    Symbol    string

    Side Side
    Type OrderType

    Quantity decimal.Decimal
    Price    decimal.Decimal

    Status OrderStatus

    CreatedAt time.Time
    UpdatedAt time.Time
}
```

Initial statuses:

```text
PENDING
OPEN
PARTIALLY_FILLED
FILLED
CANCELLED
REJECTED
FAILED
```

---

# 8. Calculate Exposure

For a long position:

```text
Exposure = Quantity × Current Price
```

Example:

```text
BTC Quantity = 0.2
BTC Price    = 100,000 USDT

Exposure     = 20,000 USDT
```

Calculate at least:

```text
Total Exposure
Symbol Exposure
Long Exposure
Short Exposure
```

---

# 9. Define Portfolio Snapshot

A snapshot represents the portfolio state at a specific point in time.

```go
type PortfolioSnapshot struct {
    AccountID string

    Equity        decimal.Decimal
    AvailableCash decimal.Decimal

    TotalExposure decimal.Decimal
    UnrealizedPnL decimal.Decimal
    RealizedPnL   decimal.Decimal

    OpenPositions int
    OpenOrders    int

    CapturedAt time.Time
}
```

This snapshot is important for later risk evaluation and historical analysis.

---

# 10. Portfolio Service Interface

```go
type PortfolioService interface {
    GetBalance(
        ctx context.Context,
        accountID string,
    ) ([]AccountBalance, error)

    GetPositions(
        ctx context.Context,
        accountID string,
    ) ([]Position, error)

    GetOpenOrders(
        ctx context.Context,
        accountID string,
    ) ([]Order, error)

    GetSnapshot(
        ctx context.Context,
        accountID string,
    ) (PortfolioSnapshot, error)
}
```

---

# 11. Portfolio Database

Suggested tables:

```text
accounts
account_balances
positions
orders
portfolio_snapshots
```

Recommended indexes:

```text
account_id
symbol
status
updated_at
```

Use database constraints for important invariants.

---

# 12. Position Lifecycle

Define the lifecycle explicitly:

```text
Candidate Trade
      ↓
Order Created
      ↓
Order Submitted
      ↓
Order Filled
      ↓
Position Opened
      ↓
Position Updated
      ↓
Position Closed
      ↓
Trade Completed
```

Do not assume:

```text
Order Created == Position Opened
```

because orders can be rejected, cancelled, delayed, or partially filled.

---

# 13. Partial Fill Handling

Support:

```text
Order
 ├── Fill 1
 ├── Fill 2
 └── Fill 3
```

Example:

```text
Requested = 1 BTC
Fill 1    = 0.4 BTC
Fill 2    = 0.6 BTC

Final position = 1 BTC
```

Average entry price should be calculated from actual fills rather than the requested order price.

---

# 14. PnL Calculation

Calculate:

```text
Unrealized PnL
Realized PnL
Gross PnL
Fees
Net PnL
```

For a simple long position:

```text
Unrealized PnL
= (Current Price - Entry Price) × Quantity
```

Final trade PnL must account for execution fees.

---

# 15. Portfolio Reconciliation

Define a reconciliation process:

```text
Internal Portfolio
        │
        │ compare
        ▼
Exchange Portfolio
```

Detect:

```text
Balance mismatch
Position mismatch
Quantity mismatch
Order status mismatch
Missing internal position
Unexpected exchange position
```

The first implementation can be basic, but the contract should exist before live trading.

---

# 16. Portfolio Idempotency

Assume events can be delivered multiple times.

Example:

```text
OrderFilled
OrderFilled
```

must not create two positions.

Use:

```text
event_id
```

and/or a business idempotency key.

Enforce uniqueness at the database level where possible.

---

# Week 4 — Risk Context Service

# 17. Risk Context Responsibilities

The Risk Context Service prepares the complete set of facts required to evaluate a Candidate Trade.

It should not call the LLM.

It should produce:

```text
Candidate
+
Portfolio
+
Trading History
+
Risk Limits
+
Market Context
=
RiskContext
```

---

# 18. Define RiskContext

```go
type RiskContext struct {
    Candidate CandidateTrade

    Portfolio PortfolioSnapshot

    PositionContext PositionRiskContext

    TradingHistory TradingHistoryContext

    RiskLimits RiskLimits

    MarketContext MarketRiskContext

    GeneratedAt time.Time
}
```

---

# 19. Position Risk Context

```go
type PositionRiskContext struct {
    ExistingPosition bool

    CurrentExposure decimal.Decimal
    SymbolExposure  decimal.Decimal

    PortfolioExposure decimal.Decimal

    OpenPositionCount int

    SameDirectionExposure decimal.Decimal
}
```

Example:

```text
Equity               = 10,000
Current BTC exposure = 2,000
New candidate        = 1,000

Potential exposure   = 3,000
```

---

# 20. Trading History Context

```go
type TradingHistoryContext struct {
    RecentTrades int

    WinningTrades int
    LosingTrades  int

    RecentPnL decimal.Decimal
    DailyPnL  decimal.Decimal

    ConsecutiveLosses int

    StrategyWinRate       decimal.Decimal
    StrategyProfitFactor  decimal.Decimal
}
```

This allows the future risk layer to know whether the strategy has recently experienced poor performance.

---

# 21. Risk Limits

```go
type RiskLimits struct {
    MaxRiskPerTrade decimal.Decimal

    MaxDailyLoss decimal.Decimal

    MaxPortfolioExposure decimal.Decimal

    MaxSymbolExposure decimal.Decimal

    MaxOpenPositions int

    MaxConsecutiveLosses int
}
```

Make these configuration-driven rather than hard-coded.

Example:

```text
Max Risk/Trade       = 1%
Max Daily Loss       = 3%
Max Portfolio Risk   = 50%
Max Symbol Exposure  = 20%
Max Open Positions   = 5
```

---

# 22. Hard Risk vs Soft Risk

This distinction should be implemented before introducing the LLM.

## Hard Risk

Must always be enforced by deterministic code:

```text
Daily loss limit exceeded
Maximum portfolio exposure exceeded
Maximum symbol exposure exceeded
Insufficient balance
Invalid order size
Missing required stop loss
Maximum position count exceeded
```

The LLM must not override these.

## Soft Risk

Can later be evaluated by the LLM:

```text
Recent strategy underperformance
Unusual volatility
Similar historical losing trades
Weak signal quality
Market regime uncertainty
```

Architecture:

```text
Candidate Trade
      ↓
Hard Risk Rules
      │
      ├── REJECT
      │
      └── PASS
            ↓
       Risk Context
            ↓
       Future LLM
```

---

# 23. Market Risk Context

Keep the first version small:

```go
type MarketRiskContext struct {
    Symbol string

    Price decimal.Decimal

    RSI float64

    BBWidth    float64
    BBPercentB float64

    Volatility float64
    VolumeRatio float64
}
```

Avoid unnecessary duplication because the Candidate Trade already contains the strategy snapshot.

---

# 24. Build RiskContext

Recommended flow:

```text
CandidateTrade
      ↓
Get Portfolio Snapshot
      ↓
Get Current Positions
      ↓
Get Recent Trading History
      ↓
Get Risk Limits
      ↓
Get Market Context
      ↓
Build RiskContext
```

Interface:

```go
type RiskContextService interface {
    Build(
        ctx context.Context,
        candidate CandidateTrade,
    ) (RiskContext, error)
}
```

---

# Week 4 — Journal Service

# 25. Journal Service Responsibilities

The Journal Service records the complete lifecycle of every candidate and trade.

The key requirement is:

> Every trading decision should be explainable after the fact.

The system should eventually answer:

```text
Why was this candidate created?
What did the strategy see?
What was the portfolio state?
What risk context existed?
What decision was made?
What happened during execution?
What was the final PnL?
```

---

# 26. Trade Lifecycle

Record these stages:

```text
Candidate Created
      ↓
Risk Context Created
      ↓
Risk Decision Created
      ↓
Order Requested
      ↓
Order Submitted
      ↓
Order Filled
      ↓
Position Opened
      ↓
Position Updated
      ↓
Position Closed
      ↓
Trade Completed
```

---

# 27. Candidate Trade Journal

Store:

```text
Candidate ID
Symbol
Timeframe
Strategy Name
Strategy Version
Side
Entry Price
Indicators
Features
Reasons
Created At
```

Do not overwrite the original indicator snapshot after creation.

---

# 28. Risk Decision Journal

Define the model now, even if the LLM does not exist yet.

```go
type RiskDecision struct {
    CandidateID string

    Decision RiskDecisionType

    ApprovedRiskAmount decimal.Decimal
    PositionSize       decimal.Decimal

    StopLossPrice  decimal.Decimal
    TakeProfitPrice decimal.Decimal

    Reasons []string

    CreatedAt time.Time
}
```

Possible decisions:

```text
APPROVE
REJECT
REDUCE
```

Initially, this can be generated by deterministic risk rules.

---

# 29. Order Journal

Store:

```text
Candidate ID
Risk Decision ID
Order ID
Client Order ID
Exchange Order ID
Symbol
Side
Quantity
Price
Order Type
Status
Created At
Submitted At
Completed At
```

Relationship:

```text
Candidate
   ↓
Risk Decision
   ↓
Order
   ↓
Fill
```

---

# 30. Fill Journal

Support multiple fills per order.

```go
type Fill struct {
    ID string

    OrderID string

    Quantity decimal.Decimal
    Price    decimal.Decimal

    FeeAsset string
    Fee      decimal.Decimal

    ExecutedAt time.Time
}
```

The final average execution price should be calculated from actual fills.

---

# 31. Trade Outcome

After a position is closed:

```go
type TradeOutcome struct {
    TradeID string

    EntryPrice decimal.Decimal
    ExitPrice  decimal.Decimal

    Quantity decimal.Decimal

    GrossPnL decimal.Decimal
    Fees     decimal.Decimal
    NetPnL   decimal.Decimal

    ReturnPercent decimal.Decimal

    HoldingDuration time.Duration

    ClosedAt time.Time
}
```

This data will later become part of the RAG/trading-history dataset.

---

# 32. Journal Database

Suggested tables:

```text
candidate_trades
risk_contexts
risk_decisions
orders
fills
trades
trade_outcomes
trading_events
```

Relationship:

```text
candidate_trades
       │
       ▼
risk_contexts
       │
       ▼
risk_decisions
       │
       ▼
orders
       │
       ▼
fills
       │
       ▼
trades
       │
       ▼
trade_outcomes
```

---

# 33. Event Correlation

Every event should contain identifiers that allow the complete lifecycle to be reconstructed.

Recommended:

```text
event_id
trace_id
candidate_id
risk_context_id
risk_decision_id
order_id
trade_id
```

Example:

```text
trace_id
   │
   ├── Candidate Trade
   ├── Risk Context
   ├── Risk Decision
   ├── Order
   ├── Fill
   └── Trade Outcome
```

---

# 34. Event Model

Recommended events:

```text
CandidateTradeCreated
RiskContextCreated
RiskDecisionCreated
OrderRequested
OrderSubmitted
OrderPartiallyFilled
OrderFilled
OrderCancelled
OrderRejected
PositionOpened
PositionUpdated
PositionClosed
TradeCompleted
```

These events can later be transported through Kafka or another event broker.

---

# 35. Idempotent Event Processing

Every consumer must be safe to retry.

Example:

```text
CandidateTradeCreated
CandidateTradeCreated
```

must not create duplicate journal records.

Use:

```text
event_id
```

with:

```text
UNIQUE(event_id)
```

where appropriate.

Use business-level idempotency keys for entities that need stronger guarantees.

---

# 36. Transaction Boundaries

Do not use one giant transaction across all services.

Prefer:

```text
Event
  ↓
Process
  ↓
Persist local state
  ↓
Publish next event
```

Each service owns its own transaction.

Example:

```text
Portfolio Service
→ owns portfolio state

Risk Context Service
→ owns risk-context generation

Journal Service
→ owns historical journal data
```

Avoid direct cross-service database writes.

---

# 37. Data Ownership

| Data | Owner |
|---|---|
| Candles | Market Data |
| Indicators | Indicator / Analysis |
| Candidate Trade | Strategy / Trading |
| Balances | Portfolio |
| Positions | Portfolio |
| Orders | Execution |
| Risk Context | Risk |
| Risk Decision | Risk |
| Trade Journal | Journal |
| Trade Outcome | Journal / Trading |

The exact service boundaries can evolve, but ownership should remain explicit.

---

# 38. Snapshot vs Live State

This distinction is critical.

## Live Portfolio State

```text
What is my portfolio now?
```

Used by:

- Dashboard
- Current risk checks
- Execution

## Historical Snapshot

```text
What did my portfolio look like when this trade was evaluated?
```

Used by:

- Journal
- Backtest analysis
- RAG
- LLM evaluation
- Post-trade analysis

Never replace historical snapshots with the latest portfolio state.

---

# 39. Recommended Go Package Structure

```text
internal/
├── portfolio/
│   ├── service.go
│   ├── account.go
│   ├── balance.go
│   ├── position.go
│   ├── order.go
│   ├── snapshot.go
│   └── repository.go
│
├── risk/
│   ├── context.go
│   ├── context_service.go
│   ├── limits.go
│   ├── position_context.go
│   ├── history_context.go
│   └── market_context.go
│
├── journal/
│   ├── candidate.go
│   ├── risk_context.go
│   ├── risk_decision.go
│   ├── order.go
│   ├── fill.go
│   ├── trade.go
│   ├── outcome.go
│   ├── event.go
│   └── repository.go
│
└── events/
    ├── candidate.go
    ├── risk.go
    ├── order.go
    └── trade.go
```

---

# 40. Suggested PostgreSQL Structure

For an MVP:

```text
portfolio
├── accounts
├── account_balances
├── positions
├── orders
└── portfolio_snapshots

risk
├── risk_limits
└── risk_contexts

journal
├── candidate_trades
├── risk_decisions
├── fills
├── trades
├── trade_outcomes
└── trading_events
```

If services use separate databases, each service should own its data instead of directly querying another service's tables.

---

# 41. Week 3 Task Breakdown

## Day 1 — Portfolio Domain

- [ ] Define Account.
- [ ] Define AccountBalance.
- [ ] Define Position.
- [ ] Define Order.
- [ ] Define PortfolioSnapshot.
- [ ] Define status enums and state transitions.

## Day 2 — Portfolio Persistence

- [ ] Create database migrations.
- [ ] Create repositories.
- [ ] Add indexes.
- [ ] Add unique constraints.
- [ ] Implement queries.

## Day 3 — Portfolio Service

- [ ] Implement balance retrieval.
- [ ] Implement position retrieval.
- [ ] Implement open-order retrieval.
- [ ] Implement exposure calculation.
- [ ] Implement portfolio snapshot.

## Day 4 — Position Lifecycle

- [ ] Implement position creation.
- [ ] Implement position updates.
- [ ] Implement position closing.
- [ ] Handle partial fills.
- [ ] Calculate realized/unrealized PnL.

## Day 5 — Reliability

- [ ] Implement idempotency.
- [ ] Add reconciliation contract.
- [ ] Add unit tests.
- [ ] Add integration tests.
- [ ] Test failure scenarios.

---

# 42. Week 4 Task Breakdown

## Day 6 — Risk Context Domain

- [ ] Define RiskContext.
- [ ] Define PositionRiskContext.
- [ ] Define TradingHistoryContext.
- [ ] Define RiskLimits.
- [ ] Define MarketRiskContext.

## Day 7 — Risk Context Service

- [ ] Retrieve portfolio snapshot.
- [ ] Retrieve current positions.
- [ ] Retrieve recent trades.
- [ ] Calculate exposure.
- [ ] Calculate daily PnL.
- [ ] Calculate consecutive losses.
- [ ] Build RiskContext.

## Day 8 — Journal Domain

- [ ] Define CandidateTrade journal.
- [ ] Define RiskDecision.
- [ ] Define Order journal.
- [ ] Define Fill.
- [ ] Define Trade.
- [ ] Define TradeOutcome.

## Day 9 — Journal Persistence

- [ ] Create database migrations.
- [ ] Implement repositories.
- [ ] Add indexes.
- [ ] Add event IDs.
- [ ] Add correlation IDs.
- [ ] Implement idempotent event processing.

## Day 10 — Integration

- [ ] Connect Candidate Trade → Portfolio.
- [ ] Connect Candidate Trade → Risk Context.
- [ ] Connect Candidate Trade → Journal.
- [ ] Persist portfolio snapshot.
- [ ] Persist risk context.
- [ ] Persist complete candidate lifecycle.
- [ ] Add end-to-end tests.

---

# 43. Testing Strategy

## Portfolio Unit Tests

```text
Balance calculation
Exposure calculation
Position PnL
Average entry price
Position lifecycle
Partial fills
```

## Risk Unit Tests

```text
Daily PnL
Portfolio exposure
Symbol exposure
Position concentration
Consecutive losses
Risk limit evaluation
```

## Journal Unit Tests

```text
Event mapping
Trade outcome calculation
Fee calculation
Idempotency
State transitions
```

---

# 44. End-to-End Test

Create a complete scenario:

```text
Candidate Trade Created
        ↓
Portfolio Snapshot Created
        ↓
Risk Context Created
        ↓
Risk Decision Created
        ↓
Order Created
        ↓
Order Filled
        ↓
Position Opened
        ↓
Position Closed
        ↓
Trade Outcome Created
```

Verify that all entities can be connected using:

```text
candidate_id
risk_context_id
risk_decision_id
order_id
trade_id
trace_id
```

---

# 45. Failure Scenarios

Test at least:

## Duplicate Event

```text
CandidateTradeCreated received twice
```

Expected:

```text
One logical journal record
```

## Partial Fill

```text
Order = 1 BTC

Fill 1 = 0.4 BTC
Fill 2 = 0.6 BTC
```

Expected:

```text
Position = 1 BTC
```

## Order Rejected

Expected:

```text
No position created
Order rejection journaled
```

## Cancelled Order

Expected:

```text
No position created
Order status = CANCELLED
```

## Service Retry

Expected:

```text
Retry does not create duplicate state
```

## Portfolio/Exchange Mismatch

Expected:

```text
Reconciliation detects mismatch
```

---

# 46. Observability

Include these fields in structured logs:

```text
trace_id
candidate_id
risk_context_id
risk_decision_id
order_id
trade_id
```

Example:

```json
{
  "event": "risk_context_created",
  "candidateId": "cnd_123",
  "riskContextId": "riskctx_456",
  "symbol": "BTCUSDT",
  "portfolioExposure": 3500,
  "symbolExposure": 2000,
  "dailyPnL": -120,
  "openPositions": 2,
  "traceId": "trace_789"
}
```

---

# 47. Data Retention and Immutability

Treat trading decisions as historical evidence.

Do not overwrite:

```text
Candidate Trade snapshot
Risk Context
Risk Decision
Portfolio Snapshot
```

after they have been recorded.

If the portfolio changes later, create a new snapshot.

This is essential for:

- Reproducibility
- Debugging
- Backtesting
- RAG
- LLM evaluation
- Post-trade analysis

---

# 48. Example Risk Context

Candidate:

```text
BTCUSDT
BUY
Entry = 107,050
RSI = 27.4
BB %B = -0.08
```

Portfolio:

```text
Equity               = 10,000 USDT
BTC Exposure         = 2,000 USDT
Total Exposure       = 3,500 USDT
Open Positions       = 2
Daily PnL             = -150 USDT
Consecutive Losses    = 2
```

Risk limits:

```text
Max Risk/Trade        = 1%
Max Daily Loss        = 300 USDT
Max Portfolio Exposure = 5,000 USDT
Max Open Positions    = 5
```

The resulting `RiskContext` contains these facts without making an LLM decision.

---

# 49. Week 3 Definition of Done

By the end of Week 3:

- [ ] Portfolio Service implemented.
- [ ] Balance model implemented.
- [ ] Position model implemented.
- [ ] Order model implemented.
- [ ] Portfolio snapshot implemented.
- [ ] Exposure calculation implemented.
- [ ] Position lifecycle implemented.
- [ ] Partial-fill handling implemented.
- [ ] PnL calculation implemented.
- [ ] Idempotency implemented.
- [ ] Basic reconciliation implemented.
- [ ] Unit tests completed.
- [ ] Integration tests completed.

The system should reliably answer:

```text
What is the portfolio state right now?
```

---

# 50. Week 4 Definition of Done

By the end of Week 4:

- [ ] Risk Context Service implemented.
- [ ] Portfolio context available.
- [ ] Trading history context available.
- [ ] Risk limits available.
- [ ] Market context available.
- [ ] RiskContext contract implemented.
- [ ] Journal Service implemented.
- [ ] Candidate lifecycle persisted.
- [ ] Risk decision persisted.
- [ ] Orders and fills persisted.
- [ ] Trade outcomes persisted.
- [ ] Correlation IDs implemented.
- [ ] Event idempotency implemented.
- [ ] End-to-end lifecycle test completed.

The system should be able to answer:

```text
Why was this candidate created?
What was the portfolio state?
What risk context existed?
What happened to the order?
What was the final result?
```

---

# 51. Final Architecture After Week 3–4

```text
                         Market Data
                              │
                              ▼
                    ┌──────────────────┐
                    │ Indicator Engine │
                    └────────┬─────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │ Strategy Engine  │
                    └────────┬─────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │ Candidate Trade │
                    └────────┬─────────┘
                             │
               ┌─────────────┼─────────────┐
               │             │             │
               ▼             ▼             ▼
        ┌────────────┐ ┌────────────┐ ┌────────────┐
        │ Portfolio  │ │   Risk     │ │  Journal   │
        │  Service   │ │  Context   │ │  Service   │
        └─────┬──────┘ │  Service   │ └─────┬──────┘
              │        └─────┬──────┘       │
              │              │              │
              ▼              ▼              ▼
        Portfolio       Risk Context    Trade History
         Snapshot
              │              │
              └───────┬──────┘
                      ▼
              Future Risk Engine
                      │
                      ▼
             Future LLM Risk Manager
```

---

# 52. What Comes After Week 3–4

The next stage should build the deterministic Risk Engine before introducing AI:

```text
Week 1–2
Indicator + Strategy
        ↓
Candidate Trade

Week 3–4
Portfolio + Risk Context + Journal
        ↓
Complete Risk Context

Week 5–6
Deterministic Risk Engine
        ↓
Approve / Reject / Reduce

Week 7+
RAG
        ↓
Similar Historical Trades

Then
LLM Risk Manager
        ↓
AI Risk Decision
```

The key milestone after Week 4 is that the system has enough structured data to evaluate every trading decision and its outcome, creating the foundation for reliable RAG and LLM-based risk management.
