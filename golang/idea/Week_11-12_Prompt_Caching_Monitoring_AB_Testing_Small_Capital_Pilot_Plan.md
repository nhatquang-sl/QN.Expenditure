# Week 11–12 Plan: Prompt Optimization, Caching, Monitoring, A/B Testing, and Small-Capital Deployment

## 1. Objective

Move the AI-assisted trading system from offline evaluation toward a controlled, observable, low-capital live pilot.

By the end of Week 12, the system should be able to:
- Optimize and version the LLM Risk Manager prompt using evidence from Week 9–10 backtests.
- Cache eligible LLM decisions safely, with explicit cache keys, expiration, and invalidation rules.
- Monitor strategy, risk decisions, execution, model quality, latency, cost, and operational health.
- Run a controlled A/B test without allowing experimental behavior to bypass deterministic hard-risk rules.
- Deploy with a small, explicitly capped amount of capital, with kill switches, reconciliation, alerts, and rollback.
- Make an evidence-based decision to continue, adjust, or stop the pilot before increasing capital.

**Important:** This is a controlled pilot, not a promise of profitability. Do not scale capital merely because the system runs reliably or a short test period is profitable.

## 2. Entry Criteria

Do not start live deployment until:
- [ ] Week 9–10 backtests for Strategy Only, Strategy + AI Risk Manager, and Strategy + AI Risk Manager + RAG are reproducible.
- [ ] The selected configuration is documented: strategy version, indicator configuration, prompt version, model identifier, risk limits, and RAG corpus/index version.
- [ ] Look-ahead bias checks have passed, including point-in-time RAG retrieval.
- [ ] The LLM returns schema-valid structured output, and invalid, missing, timed-out, or uncertain responses fail safely.
- [ ] Deterministic hard-risk controls are implemented outside the LLM and cannot be overridden.
- [ ] Order submission is idempotent, and restart/reconnect reconciliation has been tested.
- [ ] Paper-trading or shadow-mode tests include realistic fees, slippage, partial fills, and rejected orders.
- [ ] Secrets are stored outside source control and logs.
- [ ] There is a documented way to stop new orders and safely manage existing orders and positions.

If a critical criterion is missing, remain in paper/shadow mode and fix it first.

## 3. Scope and Non-Goals

### In scope
- Prompt experiments and evaluation.
- Decision cache design and correctness tests.
- Monitoring, structured logs, metrics, dashboards, and alerts.
- A/B test assignment, evaluation, and reporting.
- Small-capital live pilot with strict limits.
- Operational runbooks, rollback, and post-pilot review.

### Out of scope
- Increasing leverage to improve short-term returns.
- Automatically increasing capital after a small sample of wins.
- Letting the LLM override hard limits.
- Changing strategy, model, prompt, RAG corpus, and execution logic simultaneously without tracking each change.
- Treating backtest results as a guarantee of live performance.

## 4. System Flow and Safety Boundaries

```text
Market Data
    ↓
Candle / Indicator Engine
    ↓
Strategy Engine
    ↓
Candidate Trade
    ↓
Deterministic Hard-Risk Checks
    ↓
Risk Context + Point-in-Time RAG (if enabled)
    ↓
LLM Risk Manager (versioned prompt + model)
    ↓
Schema / Policy Validation
    ↓
Deterministic Final Risk Gate
    ↓
Execution Service
    ↓
Exchange Reconciliation
    ↓
Journal + Metrics + Alerts
```

The LLM may recommend `APPROVE`, `REJECT`, or a bounded `RESIZE` only if resizing is explicitly supported and validated. It must not override deterministic risk limits. The final risk gate must independently validate the action and size before an order is submitted.

### Required fail-safe behavior

| Condition | Required behavior |
|---|---|
| LLM timeout/provider outage | Reject the candidate or use a previously approved, explicitly permitted fallback; never bypass hard risk checks |
| Invalid JSON/schema violation | Reject the decision and do not submit the order |
| Cache unavailable | Continue without cache if policy permits; otherwise fail closed |
| Exchange state uncertain | Pause new orders until reconciliation completes |
| Daily loss/drawdown/exposure limit reached | Block new risk-taking and activate the configured protective procedure |
| Duplicate event/retry | Do not create a duplicate order |
| Monitoring or journal failure | Apply a documented fail-safe policy; never silently continue with untracked risk |

Choose and test one fallback policy before deployment. Do not change fallback behavior dynamically without a versioned configuration change.

# Week 11 — Optimize the Risk Manager and Prepare Operational Controls

## 5. Day 1: Review Backtest Evidence and Define the Baseline

### Tasks
1. Review all three scenarios:
   - A: Strategy Only
   - B: Strategy + AI Risk Manager
   - C: Strategy + AI Risk Manager + RAG
2. Select a baseline using out-of-sample performance, drawdown, risk-adjusted metrics, execution assumptions, and operational complexity—not net profit alone.
3. Categorize failures:
   - Good candidates rejected unnecessarily.
   - Weak candidates approved.
   - Position size recommended beyond policy.
   - Contradictory or unsupported reasoning.
   - Missing or stale risk context.
   - Irrelevant RAG retrieval.
   - Inconsistent output for equivalent inputs.
4. Create a frozen evaluation dataset with clear approve/reject cases, boundary-risk cases, missing/stale context, conflicting retrieved evidence, and simulated provider failures.
5. Separate prompt-development examples from a held-out evaluation set. Do not tune against the held-out set.

### Deliverables
- Baseline scenario and configuration.
- Error taxonomy and labeled evaluation dataset.
- Held-out evaluation set.
- Written rationale for baseline selection.

## 6. Day 2: Optimize and Version the Prompt

The prompt should define the LLM as a risk reviewer of an existing `CandidateTrade`, not an unrestricted market predictor or trading agent.

Include:
1. **Role:** Review the candidate and supplied risk context.
2. **Authority boundaries:** Hard limits are enforced by code and cannot be overridden.
3. **Input contract:** Field definitions, units, timestamps, freshness, and missing-value representation.
4. **Decision criteria:** Explicit approve, reject, and optional resize criteria.
5. **Evidence rules:** Use supplied facts and retrieved evidence; distinguish facts from uncertainty.
6. **Output contract:** JSON only, enumerated decisions, bounded numeric fields.
7. **Uncertainty handling:** Fail safely when essential context is missing or contradictory.
8. **Reason codes:** Stable machine-readable codes from an allowlist.
9. **No invented facts:** Never invent prices, balances, exchange state, indicators, or history.
10. **Prompt-injection resistance:** Treat retrieved documents and user-controlled text as untrusted data, not instructions that supersede system policy.

### Example response contract

```json
{
  "decision": "APPROVE",
  "reason_codes": ["RISK_WITHIN_POLICY"],
  "recommended_size": 0.0,
  "confidence": 0.0,
  "summary": "Brief explanation based on the supplied context."
}
```

This is illustrative; define exact semantics before implementation:
- `decision`: `APPROVE`, `REJECT`, or `RESIZE` only if resizing is supported.
- `recommended_size`: a quantity or risk fraction with an explicitly documented unit.
- `confidence`: diagnostic metadata only; not a substitute for deterministic validation or calibrated probability.
- `reason_codes`: fixed allowlist.
- `summary`: short bounded text; never parse business logic from it.

For `REJECT`, size should be zero. Reject unsupported actions, out-of-range sizes, unknown reason codes, and invalid types.

### Experiment method
- Change one prompt element at a time where practical.
- Use the same frozen examples and model configuration for comparisons.
- Record prompt hash/version, model identifier/version, generation settings, input version, output, latency, and token usage.
- Evaluate schema validity, decision quality, hard-policy compliance, unnecessary rejection rate, and unnecessary approval rate.
- Review examples manually; aggregate metrics can hide dangerous cases.
- Promote a prompt only if it improves agreed criteria without degrading safety-critical cases.

### Deliverables
- Versioned prompts such as `risk-prompt-v1` and `risk-prompt-v2`.
- Prompt evaluation report.
- Selected prompt and rationale.
- Regression tests for known failure cases.

## 7. Day 3: Implement Safe Decision Caching

Caching can reduce latency and model cost, but stale or incorrectly keyed decisions create financial risk.

### Cache eligibility
- Cache only deterministic outputs for identical, fully normalized decision inputs.
- Do not reuse a decision if any decision-relevant input has changed.
- Do not store provider errors, timeouts, malformed outputs, or transient failures as valid decisions.
- Consider disabling caching for time-sensitive market contexts during the pilot.
- Every cache hit must still pass current deterministic hard-risk checks and the final risk gate.

### Cache key

Canonicalize the decision inputs and hash them. Include all decision-relevant fields, such as:
- Candidate business key and strategy version.
- Symbol, timeframe, candle close time, side, entry assumptions, and indicator snapshot/version.
- Normalized risk context: balances, exposure, open positions, limits, and relevant timestamps.
- Prompt version/hash, model identifier/version, output schema version, and generation settings.
- RAG corpus/index version and retrieved document IDs/content hashes, if RAG affects the decision.
- Any configuration that changes decision semantics.

Never key only on symbol and side. Do not include secrets in the key.

### TTL and invalidation
- Use a short, configurable TTL for decisions affected by market state.
- Expire entries when relevant risk context or configuration changes.
- Invalidate on prompt/model/schema upgrades and RAG corpus/index changes.
- Store creation time, expiry time, cache-key version, and decision provenance.
- Use per-key single-flight or equivalent to avoid duplicate concurrent LLM calls.
- Provide a feature flag to disable the cache immediately.

A cached `APPROVE` is not automatically valid after portfolio exposure, balance, market price, or hard limits change.

### Tests
- Same canonical input → cache hit.
- Changed relevant field → cache miss.
- Prompt/model/schema version changed → cache miss.
- RAG document set changed → cache miss.
- Expired entry → cache miss.
- Provider error → not stored as a valid decision.
- Cache outage → documented fail-safe behavior.
- Cache hit → final risk checks still run.
- Concurrent identical requests → no duplicate unsafe order submission.

### Deliverables
- Cache interface and implementation.
- Versioned key specification.
- TTL/invalidation policy.
- Cache metrics and automated tests.

## 8. Day 4: Add Monitoring, Structured Logging, and Alerts

### Operational metrics

**Market and strategy**
- Candle/data freshness and missing-candle count.
- Candidate trades per symbol/timeframe.
- Candidate-to-decision conversion rate.
- Duplicate candidate and idempotency-conflict counts.

**LLM / RAG**
- Request count, success rate, timeout rate, schema-validation failure rate.
- Decision distribution: approve, reject, resize.
- Decision latency p50/p95/p99.
- Token usage and estimated cost per decision/day.
- Cache hit/miss rate and latency.
- RAG retrieval count, empty-retrieval rate, latency, and selected document IDs.
- Prompt/model/version distribution.

**Risk and execution**
- Hard-risk rejections by reason.
- Proposed size versus final permitted size.
- Order submission success/failure and rejection reasons.
- Partial-fill rate, slippage, fees, and fill latency.
- Exchange/local position and balance discrepancies.
- Realized/unrealized PnL, exposure, daily loss, and drawdown.

**Service health**
- Provider/API errors, queue lag, retries.
- Database/journal write failures.
- Restarts and exchange reconnects.
- Clock drift and stale configuration.

### Structured log fields

Use correlation IDs to link a candidate through risk review, order, fills, and trade outcome. Suggested fields:

```text
timestamp
trace_id
candidate_trade_id
strategy_version
prompt_version
model_version
rag_index_version
cache_hit
decision
reason_codes
input_fingerprint
latency_ms
token_usage
order_id
exchange_order_id
```

Do not log API keys, credentials, or unnecessary personal data. Prefer redacted snapshots or content hashes over full prompts where possible. Define retention and access controls for stored trading data.

### Alerts
- Daily loss/drawdown threshold reached.
- Hard exposure/position limit reached.
- Exchange/local state mismatch.
- Repeated order failures or unexpected duplicate orders.
- Stale market data.
- LLM error/timeout/schema failure rate above threshold.
- Unexpectedly high LLM cost.
- Journal/database write failures.
- Monitoring heartbeat missing.
- Unexpected prompt/model/config version change.

Configure and test thresholds; do not rely on example values.

### Deliverables
- Dashboards for system health, risk, LLM/RAG, and execution.
- Alert rules and notification route.
- Log-redaction policy.
- Test proving a simulated critical alert is delivered.

## 9. Day 5: Feature Flags, Kill Switches, and Recovery

### Required controls
Implement independently controllable flags for:
- Live order submission.
- LLM risk review.
- RAG retrieval.
- Decision cache.
- A/B experiment assignment.
- Symbol/timeframe enablement.

A kill switch must block new orders immediately. Define separately what happens to existing open orders and positions; cancelling orders and closing positions are different actions.

### Recovery and reconciliation
- On startup or exchange reconnect, reconcile balances, open orders, and positions before enabling new trades.
- Use idempotency keys for order creation and event processing.
- If submission outcome is unknown, query the exchange before retrying.
- Ensure retries cannot create duplicate orders.
- Record kill-switch activations, operator actions, configuration changes, and recovery decisions.
- Test restart during order submission and partial fill.
- Test recovery when the exchange API is unavailable.

### Deliverables
- Feature-flag configuration.
- Kill-switch behavior and runbook.
- Restart/reconnect reconciliation tests.
- Rollback procedure for prompt, model, RAG index, and application version.

# Week 12 — Controlled A/B Testing and Small-Capital Live Pilot

## 10. Day 1: Define the A/B Experiment

### Choose one primary comparison

Example:
- **Control:** Strategy + deterministic hard-risk checks, with LLM in shadow mode.
- **Treatment:** Strategy + deterministic hard-risk checks + active LLM Risk Manager.

If testing RAG's contribution, use a separate experiment or a clearly defined additional arm. Avoid changing prompt, model, cache policy, strategy, and risk limits simultaneously.

### Assignment and isolation
- Assign each eligible candidate deterministically using a stable experiment key.
- Record the variant before evaluation.
- Prevent a candidate from being evaluated in both active variants unless explicitly designed as paired offline replay.
- Keep strategy, market data, execution assumptions, hard-risk limits, and fee assumptions consistent across variants.
- Store experiment ID, variant, prompt/model/RAG versions, assignment time, and decision.
- For one live account, do not let variants independently trade the same capital concurrently. Prefer shadow evaluation or separate isolated paper portfolios first.

### Define metrics before running

**Primary metrics**
- Net PnL after fees and estimated slippage.
- Maximum drawdown.
- A risk-adjusted performance metric appropriate to the sample.
- Hard-risk policy violations (target: zero).

**Secondary metrics**
- Win rate, profit factor, average win/loss.
- Trade count and exposure time.
- Approval/rejection rates and decision changes versus control.
- Slippage, fees, latency, LLM cost, and cache hit rate.
- Operational incidents and untracked orders.

Do not choose a winner based on win rate alone. Report sample size and uncertainty. If the sample is too small, call the result inconclusive rather than claiming statistical significance.

### Deliverables
- Experiment protocol and hypothesis.
- Stable variant assignment.
- Metric definitions and acceptance criteria.
- Report template and stop conditions.

## 11. Day 2: Offline Replay and Shadow A/B

### Tasks
1. Replay the frozen held-out dataset through candidate variants.
2. Compare decisions at individual-candidate level.
3. Record where variants differ and whether each difference was beneficial, harmful, or ambiguous.
4. Run LLM shadow decisions against live market snapshots while the baseline remains responsible for any real orders.
5. Confirm shadow decisions cannot submit orders or mutate live positions.
6. Store exact prompt/model/RAG versions and normalized inputs for replay.
7. Test provider outage, malformed response, stale context, cache failure, and exchange reconnect scenarios.

### Deliverables
- Offline A/B report.
- Shadow-mode report.
- Decision-difference review.
- Evidence that shadow mode cannot submit orders.

## 12. Day 3: Paper Trading and Operational Rehearsal

Run the selected configuration in paper trading before deploying capital.

### Checklist
- [ ] Use live market data but paper orders.
- [ ] Apply realistic fees, spread, slippage, partial fills, and order rejections where supported.
- [ ] Verify closed-candle timing and prevent look-ahead.
- [ ] Correlate candidate, decision, order, fill, and outcome records end to end.
- [ ] Verify balance/position reconciliation.
- [ ] Exercise kill switch and confirm new orders are blocked.
- [ ] Exercise provider timeout and invalid-output paths.
- [ ] Exercise cache disablement and cache outage behavior.
- [ ] Confirm alerts reach the intended recipient.
- [ ] Review every trade and hard-risk rejection.
- [ ] Confirm daily loss, maximum exposure, and position limits are enforced in code.

### Go/no-go gate
Do not proceed if there is any unexplained order, duplicate order, hard-risk bypass, missing critical journal record, unreconciled exchange state, or untested kill switch.

## 13. Day 4: Deploy a Small-Capital Live Pilot

Proceed only after entry criteria, offline evaluation, shadow/paper tests, and go/no-go review pass.

### Capital and risk controls

Set explicit limits before enabling the first live order:
- Maximum total capital allocated to the pilot.
- Maximum position notional and total exposure.
- Maximum risk per trade.
- Maximum daily loss.
- Maximum drawdown before automatic pause.
- Maximum concurrent positions.
- Allowed symbols and order types.
- Leverage policy; use no leverage unless explicitly tested and approved.
- Maximum orders per time window.
- Maximum acceptable slippage.
- Maximum LLM spend per day.

Choose values appropriate to your financial circumstances and exchange/account constraints; this plan does not prescribe an amount. Treat pilot capital as money you can afford to lose. Do not use borrowed money or funds needed for living expenses.

### Deployment sequence
1. Deploy a pinned application version and configuration.
2. Verify secrets, permissions, clock synchronization, logs, dashboards, and alerts.
3. Start in read-only/shadow mode and confirm live inputs are fresh.
4. Enable order submission for the smallest permitted exposure under the configured limits.
5. Observe each candidate, LLM decision, order, fill, and reconciliation result.
6. Review after the first order and at the end of the first session.
7. Pause new orders if any critical alert, unexplained discrepancy, or policy violation occurs.

### Pilot rules
- Do not automatically increase capital.
- Do not loosen limits to recover losses.
- Do not hot-edit prompts or risk policy during a session without a versioned change and review.
- Keep a manual stop mechanism available.
- If the LLM is unavailable, follow the documented fallback; never bypass hard-risk controls.
- Distinguish disabling new orders, cancelling open orders, and closing positions.

### Deliverables
- Deployment record and exact version/configuration.
- Pilot risk-limit configuration.
- First-session checklist and review notes.
- Incident and rollback runbook.

## 14. Day 5: Review Results and Decide Whether to Continue

### Review dimensions

**Performance**
- Net PnL after fees and slippage.
- Drawdown and exposure.
- Results versus baseline and expected range.
- Number of completed trades and uncertainty due to sample size.

**Risk**
- Any hard-risk violation.
- Unexpected position sizing or exposure.
- Exchange/local state discrepancies.
- Unexplained or duplicate orders.
- Behavior during loss limits and provider outages.

**AI quality**
- Schema-valid output rate.
- Reviewed false approvals/rejections and decision patterns.
- Decision stability for identical inputs.
- Prompt/model/RAG provenance completeness.
- Cache correctness and stale-decision incidents.

**Operations**
- Alert delivery and response.
- Journal completeness.
- LLM latency and cost.
- Uptime, retries, restarts, and recovery.
- Manual intervention required.

### Decision outcomes

Choose and document one:
- **Continue at the same limit:** Risk and operational gates pass; performance is not clearly adverse; continue collecting evidence.
- **Return to paper/shadow:** Evidence is insufficient, quality is inconsistent, or an operational issue needs investigation.
- **Rollback:** A release, prompt, model, RAG, or cache change causes unacceptable behavior.
- **Stop the pilot:** Any serious risk-control failure, unexplained order, persistent reconciliation failure, or unacceptable loss threshold is reached.

Do not increase capital solely because Week 12 ends. Scaling requires a separate review after a sufficiently representative sample, evidence across market conditions, stable operations, and explicit approval of revised limits.

## 15. Suggested Go Package Structure

Adapt this to the existing repository rather than creating duplicate layers.

```text
internal/
  riskmanager/
    prompt/
      prompt.go
      versions.go
    cache/
      cache.go
      key.go
      ttl.go
    evaluation/
      dataset.go
      metrics.go
      report.go
    experiment/
      assignment.go
      variant.go
      recorder.go
  observability/
    metrics.go
    logging.go
    alerts.go
  execution/
    killswitch.go
    reconciliation.go
  config/
    featureflags.go
    risklimits.go
```

Keep the LLM provider behind an application-owned interface. Avoid coupling risk policy to a provider SDK. Make experiment assignment and prompt selection explicit in configuration and logs.

## 16. Data and Event Fields to Add

Add or verify fields needed to reconstruct each decision.

### Risk decision record
- `candidate_trade_id`
- `decision_id`
- `experiment_id`
- `variant`
- `decision`
- `reason_codes`
- `recommended_size` and unit
- `prompt_version`
- `model_version`
- `schema_version`
- `rag_index_version`
- `input_fingerprint`
- `cache_hit`
- `latency_ms`
- `token_usage`
- `created_at`

### Execution and outcome
- `order_id` and exchange order identifier
- Idempotency key
- Requested and filled quantities
- Average fill price
- Fees and estimated/realized slippage
- Exchange/local reconciliation status
- Realized PnL
- Completion timestamp

Avoid storing secrets or unredacted provider credentials. Store timestamps in UTC internally and handle exchange timestamps and candle-close times consistently.

## 17. Test Plan

### Unit tests
- Prompt version selection and fingerprinting.
- Cache-key canonicalization.
- TTL expiration and invalidation.
- Experiment assignment stability.
- Output schema and reason-code validation.
- Risk-limit enforcement independent of LLM output.
- Kill-switch state transitions.
- Metrics and alert-threshold evaluation.

### Integration tests
- LLM timeout and malformed JSON.
- Provider outage with cache enabled and disabled.
- Prompt/model/schema upgrade invalidates relevant cache entries.
- RAG version/document change invalidates cached decisions.
- Exchange timeout after order submission, followed by safe reconciliation.
- Duplicate event/retry does not create duplicate orders.
- Journal failure triggers configured fail-safe behavior.
- Restart during partial fill restores a consistent state.
- A/B variant is recorded consistently end to end.

### Replay / acceptance tests
- Re-run held-out evaluation and compare with the approved report.
- Trace every decision to exact input and version metadata.
- Confirm no variant can bypass hard-risk checks.
- Confirm live mode is disabled by default and requires explicit configuration.
- Confirm kill switch blocks new orders.
- Confirm rollback to the last approved configuration works.

## 18. Definition of Done

Week 11–12 is complete only when:
- [ ] The prompt has a versioned evaluation report and passes the held-out regression suite.
- [ ] The cache has a documented key, TTL, invalidation policy, safe failure behavior, and tests.
- [ ] Monitoring covers market data, strategy, LLM/RAG, cache, risk, execution, PnL, and service health.
- [ ] Critical alerts are tested and reach the intended recipient.
- [ ] A/B assignment and variant provenance are recorded and reproducible.
- [ ] Offline and shadow A/B reports document uncertainty and sample size.
- [ ] Paper trading and failure-mode rehearsals pass.
- [ ] Kill switch, reconciliation, idempotency, and rollback have been tested.
- [ ] The pilot has explicit capital/risk limits and a manual stop procedure.
- [ ] The live pilot is deployed within those limits or intentionally blocked by a documented no-go decision.
- [ ] A final report recommends continue, return to paper/shadow, rollback, or stop—with evidence.

## 19. Final Architecture

```text
Market Data
    ↓
Candle + Indicator Engine
    ↓
Strategy Engine
    ↓
Candidate Trade
    ↓
Deterministic Hard-Risk Gate
    ↓
Risk Context + Point-in-Time RAG
    ↓
LLM Risk Manager
    ├── Versioned Prompt
    ├── Safe Decision Cache
    ├── Schema / Policy Validation
    └── Metrics + Decision Journal
    ↓
Deterministic Final Risk Gate
    ↓
Execution Service
    ├── Idempotent Order Submission
    ├── Kill Switch
    └── Exchange Reconciliation
    ↓
Portfolio + Trade Journal
    ↓
Dashboards + Alerts + A/B Evaluation
```

## 20. Operating Principles

1. Deterministic hard-risk controls always outrank the LLM.
2. Cache correctness matters more than cache hit rate.
3. Every decision must be traceable to its input and exact versions.
4. A/B tests should isolate one meaningful change at a time.
5. Shadow mode and paper trading are required safety stages.
6. A small live pilot tests real execution and operations; it does not establish long-term profitability.
7. Never scale based on a short winning streak. Require sufficient evidence, stable operations, and an explicit risk review.
