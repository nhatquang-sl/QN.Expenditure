# Week 9–10: Backtesting and Comparative Evaluation Plan

## 1. Objective

Build a reproducible backtesting workflow to compare three configurations using the same historical data, candidate trades, execution assumptions, and deterministic hard-risk rules.

1. **Scenario A — Strategy Only:** deterministic strategy + deterministic hard-risk rules.
2. **Scenario B — Strategy + AI Risk Manager:** strategy + hard-risk rules + LLM Risk Manager, without RAG.
3. **Scenario C — Strategy + AI Risk Manager + RAG:** strategy + hard-risk rules + LLM Risk Manager with retrieved historical context.

The goal is to measure whether AI and RAG add value beyond the baseline. Do not assume that either improves performance before testing. The LLM remains an evaluation component; this phase must not enable autonomous live trading.

## 2. Core Principles

### Fair comparison

All scenarios must use the same:
- Historical candle dataset and evaluation dates
- Strategy name/version and indicator calculations
- Candidate generation rules
- Initial capital and position sizing rules
- Fees, slippage, fill and exit assumptions
- Portfolio simulation and deterministic hard-risk limits
- Metric definitions

Only the intended AI/RAG decision inputs should differ.

### Avoid look-ahead bias

- Use closed candles only.
- Do not provide future candles, future PnL, or eventual trade outcomes to the LLM.
- RAG must exclude documents whose outcomes were not yet known at the simulated decision time.
- Do not tune prompts, thresholds, retrieval settings, or strategy parameters on the final test period.
- Use point-in-time filters such as `available_at` or `outcome_known_at`.

### Hard risk remains deterministic

A hard-risk rejection must remain a rejection in every scenario. The LLM cannot override exposure, loss, position-size, or other hard limits.

### Separate decisions from outcomes

Record decisions and simulated outcomes separately. A rejected candidate is not a completed trade with zero PnL. Track rejected candidates and, optionally, their hypothetical outcomes in a separate analysis dataset.

## 3. Scope

### In scope
- Historical data selection and validation
- Reproducible backtest runner
- Frozen candidate-trade dataset
- Three scenario decision paths
- Point-in-time RAG retrieval
- LLM response caching and replay
- Shared execution and portfolio simulator
- Fees, slippage, and fill assumptions
- Performance and risk metrics
- Comparative report and visualizations
- Robustness and walk-forward evaluation
- Reproducibility metadata and regression tests

### Out of scope
- Autonomous real-money execution
- Automatic strategy tuning based on test results
- Online learning from test-period outcomes
- LLM fine-tuning
- Treating LLM confidence as calibrated win probability
- Enabling the LLM to modify hard risk limits

## 4. Three Scenarios

### Scenario A — Strategy Only

```text
Historical Candles
      ↓
Indicator Engine
      ↓
Strategy Engine
      ↓
CandidateTrade
      ↓
Deterministic Hard Risk
      ↓
Common Simulated Execution
      ↓
Portfolio / Journal
      ↓
Metrics
```

Purpose: establish the baseline and validate the backtest engine independently of AI.

### Scenario B — Strategy + AI Risk Manager (no RAG)

```text
Historical Candles
      ↓
Indicator Engine
      ↓
Strategy Engine
      ↓
CandidateTrade
      ↓
Deterministic Hard Risk
      ↓
LLM Risk Manager (no retrieved documents)
      ↓
Decision Validation
      ↓
Common Simulated Execution
      ↓
Portfolio / Journal
      ↓
Metrics
```

The LLM may receive the candidate, current risk context, market context, and constraints available at that simulated time, but no retrieved historical trade documents.

### Scenario C — Strategy + AI Risk Manager + RAG

```text
Historical Candles
      ↓
Indicator Engine
      ↓
Strategy Engine
      ↓
CandidateTrade
      ↓
Deterministic Hard Risk
      ↓
Point-in-Time RAG Retrieval
      ↓
LLM Risk Manager
      ↓
Decision Validation
      ↓
Common Simulated Execution
      ↓
Portfolio / Journal
      ↓
Metrics
```

Record retrieved document IDs, similarity scores, filters, and index/embedding versions. Exclude future outcomes and the current candidate's own future record.

### Interpreting the differences

- **B − A:** observed incremental difference from adding AI without RAG.
- **C − B:** observed incremental difference from adding RAG to AI.
- **C − A:** observed total difference from the AI + RAG configuration.

These are empirical differences for the selected data and assumptions, not proof of future performance or causality.

## 5. Week 9 — Build the Backtest Foundation

### Day 1: Define the backtest contract

Example:

```go
type BacktestConfig struct {
    RunID           string
    Symbol          string
    Timeframe       string
    StartTime       time.Time
    EndTime         time.Time
    InitialCapital  decimal.Decimal
    FeeRate         decimal.Decimal
    SlippageBps     decimal.Decimal
    StrategyName    string
    StrategyVersion string
    Scenario        Scenario
    DatasetVersion  string
}

type Scenario string

const (
    ScenarioStrategyOnly Scenario = "STRATEGY_ONLY"
    ScenarioAI           Scenario = "STRATEGY_AI"
    ScenarioAIRAG        Scenario = "STRATEGY_AI_RAG"
)
```

Validate timestamps, capital, costs, and scenario. Store all configuration values with each run. Do not vary execution-cost assumptions across scenarios unless conducting a separate experiment.

### Day 1–2: Validate historical data

Create a dataset manifest:

```text
dataset_id
symbol
timeframe
start_time
end_time
source
downloaded_at
candle_count
missing_candle_count
duplicate_candle_count
data_hash
```

Checks:
- Candles are ordered by close time.
- Duplicates and gaps are detected and documented.
- OHLC values are valid.
- Timestamps and timezone conventions are consistent.
- Only closed candles generate decisions.
- Indicator warm-up periods are excluded from scoring.
- Dataset integrity can be checked again using the manifest.

Do not silently fill missing candles. If filling is necessary, document the policy and apply it consistently.

### Day 2: Freeze candidate generation

Generate candidates once using the fixed dataset and strategy version. Persist:

```text
candidate_trade_id
symbol
timeframe
candle_close_time
strategy_name
strategy_version
side
entry_reference_price
indicator_snapshot
strategy_reasons
simulated_decision_time
```

Requirements:
- Candidate generation is independent of the scenario.
- Identical market input and strategy version produce the same candidate set.
- Use a stable idempotency key, such as symbol + timeframe + candle close time + strategy version.
- AI decisions must not change which raw strategy candidates are generated.

### Day 2–3: Implement the backtest runner

Example interface:

```go
type BacktestRunner interface {
    Run(
        ctx context.Context,
        config BacktestConfig,
    ) (BacktestResult, error)
}
```

The runner should:
1. Load and validate data.
2. Warm up indicators.
3. Generate or load the frozen candidate set.
4. Build point-in-time risk context.
5. Apply deterministic hard-risk rules.
6. Run the selected scenario decision path.
7. Simulate fills and position lifecycle.
8. Record trade outcomes and portfolio equity.
9. Calculate metrics.
10. Persist results and reproducibility metadata.

Support a deterministic fake LLM provider for automated tests.

### Day 3: Implement one common execution simulator

Define and document:
- Entry timing (for example, next candle open after the signal candle closes)
- Stop-loss, take-profit, strategy exit, or time-based exit rules
- Partial-fill behavior, if modelled
- Entry and exit fee application
- Slippage model
- Gaps through stop levels
- Position sizing and quantity rounding
- Simultaneous candidate handling
- Maximum open positions
- End-of-period open-position handling

Use the same simulator for all scenarios. Avoid assuming a fill at the signal candle close when the signal is only known after that candle closes, unless explicitly justified.

### Day 3–4: Implement portfolio simulation

Track at minimum:

```text
cash / available balance
equity
open positions
position quantity
entry price
mark price
unrealized PnL
realized PnL
fees
exposure
drawdown
```

Ensure updates are deterministic, PnL is not double-counted, rejected candidates are recorded separately, and mark-to-market rules are consistent.

### Day 4: Validate Scenario A first

Implement Strategy Only and validate:
- Candidate set matches the frozen dataset.
- Fills follow the execution policy.
- Fees and slippage are applied correctly.
- Portfolio accounting reconciles.
- A small hand-calculated fixture produces expected results.

Do not compare AI results until the baseline is credible.

### Day 4–5: Integrate Scenario B

Connect the LLM Risk Manager without RAG. Include only information available at the decision time.

Requirements:
- No historical trade documents in the prompt.
- Reuse Week 7–8 JSON/schema/domain validation.
- Record provider errors and timeouts.
- Never silently convert a failed AI call into APPROVE.
- Document a consistent failure policy. A conservative research policy may map failure to `REVIEW` and skip the candidate in Scenario B/C.
- Hard-risk rejection remains authoritative.

#### LLM response cache and replay

Cache decisions using a key that includes all relevant inputs and versions:

```text
candidate_trade_id
risk_context_hash
scenario
model/version
prompt_version
schema_version
input_hash
```

Reuse a cached response only when the complete relevant key matches. Record whether the response was generated or replayed from cache.

### Day 5: Integrate Scenario C with point-in-time RAG

For every retrieval request, record:

```text
retrieval_query
simulated_decision_time
max_outcome_known_at
retrieved_document_ids
similarity_scores
metadata_filters
embedding_model_version
index_version
top_k
```

Requirements:
- Retrieve only documents available by the simulated decision time.
- Exclude the candidate's own future outcome and duplicate/future versions.
- Keep embedding model and index versions fixed within a run.
- Record retrieval failures and empty results.
- Missing RAG context must not silently mean APPROVE.
- A trade outcome document becomes retrievable only after the outcome was known and the document could realistically have been indexed.

## 6. Week 10 — Metrics and Comparative Evaluation

### Day 6: Define metric contracts

Document formulas, units, and denominators for each metric.

#### Performance
- Net PnL after fees and slippage
- Total return
- Annualized return, only when period and sampling assumptions support it
- Number of executed trades
- Win rate
- Average win and average loss
- Profit factor
- Expectancy per executed trade
- Average holding duration
- Exposure utilization

#### Risk
- Maximum drawdown
- Periodic-return volatility
- Sharpe ratio with documented risk-free rate and sampling frequency
- Sortino ratio, if its downside-return calculation is documented
- Largest loss
- Consecutive losses
- Time in market
- Peak exposure
- Hard-risk rejection count

#### AI/RAG
- APPROVE / REJECT / REVIEW rates
- AI rejection rate among candidates passing hard risk
- Invalid JSON rate
- Schema-validation failure rate
- LLM timeout/error rate
- Fallback rate
- Average and P95 latency
- Input/output tokens and estimated cost
- RAG empty-result rate and latency
- Evidence-ID validation failure rate
- Average retrieved document count
- Decision changes from Scenario B to Scenario C

Do not compare win rate alone. A higher win rate can coexist with worse expectancy or drawdown.

### Day 6–7: Implement metric calculations

For a closed trade:

```text
NetPnL = GrossPnL - EntryFees - ExitFees - SlippageCost
```

For a long position:

```text
GrossPnL = (ExitPrice - EntryPrice) × Quantity
```

For a short position:

```text
GrossPnL = (EntryPrice - ExitPrice) × Quantity
```

Adapt formulas to the exchange's contract and quantity conventions.

Profit factor:

```text
Profit Factor = Sum(Profits) / Abs(Sum(Losses))
```

Define behavior when there are no losses; prefer a nullable value over silently reporting an arbitrary finite value.

Expectancy:

```text
Expectancy = (WinRate × AverageWin) - (LossRate × AverageLoss)
```

Document how breakeven trades are counted.

Drawdown:

```text
Drawdown(t) = (Equity(t) - PeakEquity(t)) / PeakEquity(t)
MaxDrawdown = minimum Drawdown(t)
```

Use the same equity sampling interval for every scenario.

### Day 7: Generate the comparative report

Create machine-readable results and a human-readable report.

| Metric | A: Strategy | B: Strategy + AI | C: Strategy + AI + RAG |
|---|---:|---:|---:|
| Net PnL | | | |
| Total return | | | |
| Maximum drawdown | | | |
| Executed trades | | | |
| Rejected candidates | | | |
| Win rate | | | |
| Expectancy per trade | | | |
| Profit factor | | | |
| Sharpe ratio | | | |
| Fees + slippage | | | |
| Average latency | N/A | | |
| Estimated AI cost | 0 | | |
| Invalid JSON rate | N/A | | |
| RAG empty-result rate | N/A | N/A | |

Also include:
- Candidate count before risk decisions
- Candidates passing hard risk
- AI approvals, rejections, and reviews
- Actual simulated executions
- LLM/RAG failures
- Equity and drawdown curves
- PnL distribution
- Results by market regime, if regimes are defined without future leakage

Populate the report only from actual runs; do not invent illustrative performance numbers.

### Day 7–8: Analyse changed decisions

Compare Scenario B and C per candidate:

```text
candidate_trade_id
scenario_b_decision
scenario_c_decision
scenario_b_reason_codes
scenario_c_reason_codes
scenario_b_retrieved_docs
scenario_c_retrieved_docs
eventual_hypothetical_outcome
```

Investigate which decisions changed, whether retrieved documents were relevant, whether evidence IDs were valid, and whether changes affected net PnL or drawdown. Eventual outcomes are for post-decision analysis only and must never be model input.

### Day 8: Robustness testing

Where data permits:
- Separate training/development, validation, and out-of-sample periods.
- Run walk-forward evaluation.
- Compare independently defined market regimes.
- Sensitivity-test reasonable fee and slippage assumptions.
- Test different time windows.
- Calculate confidence intervals using an appropriate bootstrap method, documenting assumptions.
- Check subperiod consistency.
- Ensure scenarios are compared on exactly matching dates.

Do not tune on the final out-of-sample period. If you change prompts, thresholds, or retrieval settings after inspecting test results, treat that period as validation and reserve a new holdout.

### Day 8–9: Test failure and fallback policies

Test:
- LLM timeout and rate limit
- Provider unavailable
- Invalid JSON or schema mismatch
- Invalid evidence IDs
- RAG timeout or empty results
- Future-dated or stale documents
- Duplicate candidate events
- Restart and replay of an existing run

Failure policy must be explicit and consistent. Never silently convert AI failure into APPROVE.

### Day 9: Reproducibility and regression tests

Store with every run:

```text
run_id
scenario
dataset_id and data_hash
config_hash
strategy_version
indicator_config_version
execution_model_version
hard_risk_rules_version
provider/model/version
prompt_version
decision_schema_version
embedding_model_version
RAG index version
retrieval configuration
software commit
run_started_at
run_completed_at
```

Verify:
- Same deterministic inputs produce the same candidate set.
- Same fake LLM responses produce the same portfolio results.
- Replaying cached decisions with identical inputs reproduces the run.
- Different prompt/model/input hashes do not reuse incompatible cache entries.
- Results can be traced to candidates, decisions, fills, and outcomes.

Live LLM outputs may vary even at low temperature. Persist the exact output used in each run and measure model variability separately if needed.

### Day 10: Final evaluation and go/no-go review

Review:
1. Is Scenario A independently validated?
2. Are execution assumptions realistic and identical?
3. Are look-ahead and survivorship biases controlled?
4. Is RAG point-in-time safe?
5. Are fees and slippage included?
6. Are AI failures handled consistently?
7. Are results robust across subperiods?
8. Does any improvement remain after costs?
9. Does RAG add value beyond AI without RAG?
10. Is evidence strong enough to justify another research phase?

Do not enable live trading simply because one scenario has the highest backtest return.

## 7. Suggested Go Package Structure

```text
internal/
├── backtest/
│   ├── config.go
│   ├── runner.go
│   ├── scenario.go
│   ├── result.go
│   ├── dataset.go
│   ├── execution.go
│   ├── portfolio.go
│   ├── metrics.go
│   ├── report.go
│   └── replay.go
├── evaluation/
│   ├── comparison.go
│   ├── robustness.go
│   ├── walkforward.go
│   └── fixtures.go
├── riskmanager/
│   └── ...
├── rag/
│   └── ...
├── strategy/
│   └── ...
└── journal/
    └── ...
```

Prefer a replaceable scenario decision interface rather than scattering scenario-specific conditionals throughout strategy and indicator code.

```go
type CandidateDecisionProvider interface {
    Decide(
        ctx context.Context,
        candidate CandidateTrade,
        riskContext RiskContext,
    ) (DecisionResult, error)
}
```

Implementations:

```text
StrategyOnlyDecisionProvider
AIDecisionProvider
AIRAGDecisionProvider
```

The runner and execution simulator should be shared.

## 8. Suggested Test Layout

```text
testdata/
├── candles/
│   └── btcusdt_5m_sample.csv
├── candidates/
│   └── candidate_set_v1.json
├── llm_responses/
│   ├── approve.json
│   ├── reject.json
│   ├── review.json
│   └── invalid.json
├── rag/
│   ├── point_in_time_docs.json
│   └── future_document_should_be_excluded.json
└── expected/
    ├── scenario_a_small_fixture.json
    ├── scenario_b_small_fixture.json
    └── scenario_c_small_fixture.json
```

Test categories:
- PnL, fees, slippage, drawdown, and metric formulas
- Dataset validation
- Candidate-set determinism
- Scenario routing
- Point-in-time RAG filtering
- LLM failure/fallback
- End-to-end backtest with fixed fixtures
- Report serialization
- Reproducibility and cache keys

## 9. Suggested Data Models

### BacktestRun

```go
type BacktestRun struct {
    ID              string
    Scenario        Scenario
    DatasetID       string
    ConfigHash      string
    StrategyVersion string
    StartedAt       time.Time
    CompletedAt     time.Time
    Status          string
}
```

### BacktestTradeResult

```go
type BacktestTradeResult struct {
    CandidateTradeID string
    Scenario         Scenario
    Decision         string
    EntryTime        time.Time
    ExitTime         *time.Time
    EntryPrice       decimal.Decimal
    ExitPrice        *decimal.Decimal
    Quantity         decimal.Decimal
    GrossPnL         decimal.Decimal
    Fees             decimal.Decimal
    SlippageCost     decimal.Decimal
    NetPnL           decimal.Decimal
    ExitReason       string
}
```

### ScenarioDecisionRecord

```go
type ScenarioDecisionRecord struct {
    CandidateTradeID string
    Scenario         Scenario
    Decision         string
    ReasonCodes      []string
    RetrievedDocIDs  []string
    InputHash        string
    OutputHash       string
    ModelVersion     string
    PromptVersion    string
    CreatedAt        time.Time
}
```

Adapt these examples to existing domain types instead of duplicating models unnecessarily.

## 10. Definition of Done

### Backtest foundation
- [ ] Dataset manifest and integrity checks exist.
- [ ] Warm-up and missing-data policies are documented.
- [ ] Candidates are generated from a frozen strategy version.
- [ ] Runner supports all three scenarios.
- [ ] One shared execution simulator is used.
- [ ] Portfolio accounting matches a hand-calculated fixture.
- [ ] Fees and slippage are included.

### Scenario fairness
- [ ] Same dataset and dates for every scenario.
- [ ] Same candidate generation and strategy version.
- [ ] Same execution assumptions and hard-risk rules.
- [ ] Only intended AI/RAG inputs differ.
- [ ] AI failure policy is documented and applied consistently.

### AI and RAG
- [ ] Scenario B uses no retrieved historical documents.
- [ ] Scenario C uses point-in-time RAG.
- [ ] Retrieved IDs and scores are stored.
- [ ] Future outcomes cannot enter prompts.
- [ ] LLM output is schema/domain validated.
- [ ] Exact model, prompt, and output are recorded.
- [ ] Hard risk cannot be overridden.

### Metrics and evaluation
- [ ] Net PnL includes fees and slippage.
- [ ] Metric formulas and denominators are documented.
- [ ] Rejected candidates are separate from executed trades.
- [ ] Report compares all three scenarios.
- [ ] AI cost and latency are measured.
- [ ] Robustness or walk-forward analysis is completed where feasible.
- [ ] Final holdout is not reused for tuning.

### Reproducibility and safety
- [ ] Data hashes and configuration are stored.
- [ ] Results can be replayed and audited.
- [ ] LLM/RAG failure tests pass.
- [ ] Backtest runner cannot place real orders.
- [ ] No live trading is enabled by this phase.

## 11. Deliverables

By the end of Week 9–10, produce:
1. Historical dataset manifest and validation report.
2. Versioned backtest configuration.
3. Frozen candidate-trade dataset.
4. Common execution simulator.
5. Portfolio simulation and trade lifecycle.
6. Scenario A implementation.
7. Scenario B implementation.
8. Scenario C implementation with point-in-time RAG.
9. LLM decision cache and replay support.
10. Metric calculation module.
11. Comparative report generator.
12. Equity and drawdown curves.
13. Robustness/walk-forward report.
14. Regression and integration tests.
15. Reproducibility metadata and run manifest.
16. Written interpretation of results and limitations.

## 12. Final Architecture After Week 10

```text
Frozen Historical Dataset
          ↓
Dataset Validation + Manifest
          ↓
Indicator Engine
          ↓
Strategy Engine
          ↓
Shared CandidateTrade Set
          ↓
Shared Hard Risk Rules
          ↓
Scenario Decision Provider
    ┌─────────────┼────────────────┐
    ↓             ↓                ↓
Scenario A     Scenario B       Scenario C
Strategy Only  Strategy + AI    Strategy + AI + RAG
    └─────────────┼────────────────┘
                  ↓
        Common Execution Simulator
                  ↓
         Portfolio / Trade Journal
                  ↓
           Metric Calculation
                  ↓
       Comparative + Robustness Report
```

## 13. Key Success Criteria

Week 9–10 succeeds when the project can answer with traceable evidence:
- Does the AI Risk Manager improve or worsen baseline results?
- Does RAG add measurable value beyond AI without RAG?
- Are differences robust across time periods and reasonable execution assumptions?
- Do improvements remain after fees, slippage, and AI costs?
- Can every result be reproduced from stored data, configuration, decisions, and versions?
- Is there evidence of look-ahead bias or information leakage?

The result does not need to show that AI wins. A credible finding that AI or RAG provides no improvement is still useful.

> The objective is not to prove that AI is better. The objective is to measure whether AI and RAG add robust, reproducible value under fair backtesting conditions.
