# CheckSignalStopLoss

## Overview

`CheckSignalStopLossCommand` runs every minute inside `FindSignalService` and detects when active leveraged positions have been stopped out. It queries up to 100 `SignalRecord` rows where `EntryHitAt IS NOT NULL AND StopLossHitAt IS NULL`, fetches 1-minute BTCUSDT candles in batches starting from each signal's `LastCheckedCandleAt` pointer (anchored to `EntryHitAt` by `CheckSignalEntry`), and marks `StopLossHitAt` when the market price moves 70% of margin against the position (scaled by leverage). While the position is open, the `StopLoss` field is updated in-flight to track the worst observed adverse price, enabling analytics to show proximity-to-stop without replaying historical candles.

**Module Location**: `src/Cex/Cex.Application/Signal/Commands/CheckSignalStopLoss/`
**Scope**: BTCUSDT only; all signal intervals

---

## Data Model

### Entity: `SignalRecord` (Existing — modified)

New column:

| Column   | Type | Nullable | Default | Constraints                          | Notes                                               |
|----------|------|----------|---------|--------------------------------------|-----------------------------------------------------|
| Leverage | int  | No       | 10      | CK: `Leverage >= 1 AND Leverage <= 125` | Position leverage multiplier; used to compute the dynamic stop-loss threshold |

Updated entity definition:

```csharp
public class SignalRecord
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public SignalType SignalType { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime PreviousCandleAt { get; set; }
    public decimal RsiValue { get; set; }
    public decimal PreviousRsiValue { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public int Leverage { get; set; } = 10;
    public DateTime? EntryHitAt { get; set; }
    public DateTime? StopLossHitAt { get; set; }
    public DateTime? TakeProfitHitAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastCheckedCandleAt { get; set; }
}
```

### Business Rules

1. **Stop-loss threshold** is computed from `EntryPrice` and `Leverage` once per command run; it is constant for a given signal's lifetime.
   - Long: `stopLossPrice = EntryPrice × (1 − 0.70 / Leverage)`
   - Short: `stopLossPrice = EntryPrice × (1 + 0.70 / Leverage)`

   Example — Long, entry = 100, leverage = 10:
   `stopLossPrice = 100 × (1 − 0.07) = 93` → hit when any 1-min candle has low ≤ 93.

2. **Hit condition** — stop-loss is triggered when a 1-minute candle's extreme price crosses `stopLossPrice`:
   - Long: `candle.LowestPrice ≤ stopLossPrice`
   - Short: `candle.HighestPrice ≥ stopLossPrice`

3. **`StopLoss` field tracking** — while open, `StopLoss` is overwritten with the running worst adverse price across all candles processed since the previous run:
   - Long: `StopLoss = Math.Min(StopLoss, candle.LowestPrice)`
   - Short: `StopLoss = Math.Max(StopLoss, candle.HighestPrice)`

   Tracking applies to `relevantCandles` (those with `OpenTime > signal.LastCheckedCandleAt`) up to and including the hit candle. Once `StopLossHitAt` is set, `StopLoss` is frozen.

4. **`StopLoss` field repurposing** — `StopLoss` is initially written by `FindSignalCommandHandler` as a static divergence-based reference price (Long: `entryPrice × 0.92`, Short: `entryPrice × 1.08`). `CheckSignalStopLoss` intentionally overwrites this value with the running worst-observed price. The original divergence-based value is not preserved; once a position is entered, live proximity-to-stop is operationally more useful.

5. **`Leverage` default** — existing rows without an explicit value default to 10 (most common trading configuration).

6. **`Leverage` range** — values outside 1–125 are rejected by a database check constraint.

7. **Pointer monotonicity** — `LastCheckedCandleAt` must never move backward. For unhit signals it is updated to `Max(LastCheckedCandleAt, lastCandleOpenTime)` at the end of each run.

---

## Algorithm

```
1. Query 100 SignalRecords:
     WHERE EntryHitAt IS NOT NULL AND StopLossHitAt IS NULL
     ORDER BY LastCheckedCandleAt ASC
2. If candidates is empty → return early (no KuCoin call)
3. startAt       = Min(candidates, s => s.LastCheckedCandleAt)
   now            = DateTime.UtcNow
   unhitCount     = candidates.Count
   lastCandleOpenTime = null

4. Pre-compute stopLossPrice per signal (constant for this run):
     Long:  EntryPrice × (1 − 0.70 / Leverage)
     Short: EntryPrice × (1 + 0.70 / Leverage)

5. while startAt < now:
   a. batch = GetKlines("BTCUSDT", 1min, startAt, interval.GetEndDate(startAt))
      (returns ≤ 1500 candles sorted ascending by OpenTime)
   b. if batch is empty → break
   c. lastCandleOpenTime = batch[^1].OpenTime
      startAt = lastCandleOpenTime + 1 min      ← advances the shared window pointer

   d. for each signal in candidates where StopLossHitAt is null:
        relevantCandles = batch WHERE OpenTime > signal.LastCheckedCandleAt, ORDER BY OpenTime ASC
        if none → skip this signal for this batch

        stopLossPrice = pre-computed value for signal.Id
        hit = relevantCandles.FirstOrDefault(hit condition)

        candlesToTrack = (hit is null) ? relevantCandles
                                       : relevantCandles.TakeWhile(c => c.OpenTime <= hit.OpenTime)
        for each c in candlesToTrack:
          Long:  signal.StopLoss = Math.Min(signal.StopLoss, c.LowestPrice)
          Short: signal.StopLoss = Math.Max(signal.StopLoss, c.HighestPrice)

        if hit is not null:
          signal.StopLossHitAt       = hit.OpenTime
          signal.LastCheckedCandleAt = hit.OpenTime
          unhitCount--

   e. if unhitCount == 0 → break (all signals stopped out)

6. if lastCandleOpenTime is null → return (no DB write — no batch was fetched)

7. For each signal in candidates WHERE StopLossHitAt IS NULL
                                 AND lastCandleOpenTime > signal.LastCheckedCandleAt:
     signal.LastCheckedCandleAt = lastCandleOpenTime
     (never move the pointer backwards in case of an early-exit gap)

8. SaveChangesAsync
```

> **Why pre-compute `stopLossPrice`?**
> `EntryPrice` and `Leverage` are constants for a given signal. Pre-computing into a dictionary avoids redundant decimal arithmetic inside the inner loop and makes each lookup O(1).

> **Why track `StopLoss` candle-by-candle up to the hit?**
> Processing candle-by-candle with `TakeWhile(c => c.OpenTime <= hit.OpenTime)` ensures tracking stops precisely at the hit candle and never includes candles from after the stop was triggered. Since `relevantCandles` is sorted ascending, `TakeWhile` on `OpenTime` is safe and includes the hit candle itself.

> **Why use `Max(LastCheckedCandleAt, lastCandleOpenTime)` in step 7?**
> If all signals are stopped out before the final batch (`unhitCount == 0` break in step 5e), `lastCandleOpenTime` may be earlier than some signals' existing `LastCheckedCandleAt`. The `Max` guard prevents regressing the pointer for any signal.

---

## Data Access

**Step 1 — Load candidates:**

```csharp
var candidates = await dbContext.SignalRecords
    .Where(s => s.EntryHitAt != null && s.StopLossHitAt == null)
    .OrderBy(s => s.LastCheckedCandleAt)
    .Take(100)
    .ToListAsync(cancellationToken);

if (candidates.Count == 0) return;
```

**Steps 3–8 — Candle loop, adverse tracking, hit detection:**

```csharp
const IntervalType interval = IntervalType.OneMinute;
var startAt = candidates.Min(s => s.LastCheckedCandleAt);
var now = DateTime.UtcNow;
DateTime? lastCandleOpenTime = null;
var unhitCount = candidates.Count;

// Pre-compute per-signal stop-loss prices.
// EntryPrice, Leverage, and SignalType are constants for the lifetime of this command.
// 0.70m / s.Leverage: s.Leverage is int; C# promotes to decimal for the division — no integer truncation.
var stopLossPrices = candidates.ToDictionary(
    s => s.Id,
    s => s.SignalType == SignalType.Long
        ? s.EntryPrice * (1 - 0.70m / s.Leverage)
        : s.EntryPrice * (1 + 0.70m / s.Leverage));

try
{
    while (startAt < now)
    {
        var batch = await kuCoinService.GetKlines(
            "BTCUSDT", interval, startAt, interval.GetEndDate(startAt), kuCoinConfig.Value);

        if (batch.Count == 0) break;

        lastCandleOpenTime = batch[^1].OpenTime;
        startAt = lastCandleOpenTime.Value.AddMinutes(1); // advance shared window pointer

        foreach (var signal in candidates.Where(s => s.StopLossHitAt == null))
        {
            var checkFrom = signal.LastCheckedCandleAt;
            var stopLossPrice = stopLossPrices[signal.Id];

            // GetKlines returns candles sorted ascending by OpenTime.
            // The explicit OrderBy is a defensive guard against contract changes.
            var relevantCandles = batch
                .Where(c => c.OpenTime > checkFrom)
                .OrderBy(c => c.OpenTime)
                .ToList();

            if (relevantCandles.Count == 0) continue;

            var hit = relevantCandles.FirstOrDefault(c =>
                signal.SignalType == SignalType.Long
                    ? c.LowestPrice  <= stopLossPrice
                    : c.HighestPrice >= stopLossPrice);

            var candlesToTrack = hit is null
                ? (IEnumerable<KlineDto>)relevantCandles
                : relevantCandles.TakeWhile(c => c.OpenTime <= hit.OpenTime);

            foreach (var c in candlesToTrack)
            {
                signal.StopLoss = signal.SignalType == SignalType.Long
                    ? Math.Min(signal.StopLoss, c.LowestPrice)
                    : Math.Max(signal.StopLoss, c.HighestPrice);
            }

            if (hit is null) continue;

            signal.StopLossHitAt       = hit.OpenTime;
            signal.LastCheckedCandleAt = hit.OpenTime;
            unhitCount--;
        }

        if (unhitCount == 0) break;
    }
}
catch (Exception ex)
{
    // Any exception (rate-limit 429, network, timeout) during the loop:
    // fall through and save whatever progress was accumulated before the failure.
    logTrace.LogError("CheckSignalStopLoss loop interrupted — saving partial progress", ex);
}

if (lastCandleOpenTime is null) return;

foreach (var signal in candidates.Where(s =>
    s.StopLossHitAt == null && lastCandleOpenTime.Value > s.LastCheckedCandleAt))
{
    signal.LastCheckedCandleAt = lastCandleOpenTime.Value;
}

await dbContext.SaveChangesAsync(cancellationToken);
```

---

## Backend Architecture

### Domain Layer

**Modified:** `src/Cex/Cex.Domain/Entities/SignalRecord.cs`
- Add `public int Leverage { get; set; } = 10;`

### Infrastructure Layer

**Modified:** `src/Cex/Cex.Infrastructure/Data/Configurations/SignalRecordConfiguration.cs`

```csharp
builder.Property(x => x.Leverage)
    .HasDefaultValue(10);
builder.HasCheckConstraint("CK_SignalRecords_Leverage", "[Leverage] >= 1 AND [Leverage] <= 125");
```

**Migration name:** `AddSignalRecordLeverage`

### Application Layer

**New file:** `src/Cex/Cex.Application/Signal/Commands/CheckSignalStopLoss/CheckSignalStopLossCommand.cs`

```csharp
using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Signal.Commands.CheckSignalStopLoss;

public record CheckSignalStopLossCommand : IRequest;

public class CheckSignalStopLossCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalStopLossCommand>
{
    public async Task Handle(CheckSignalStopLossCommand command, CancellationToken cancellationToken)
    {
        // ... (see Data Access section)
    }
}
```

### Hosted Service

**Modified:** `src/WebAPI/HostedServices/FindSignalService.cs`

Add a `CheckSignalStopLoss` private method (same pattern as `CheckSignalEntry`) and call it in `ExecuteAsync` immediately after `CheckSignalEntry`:

```csharp
private async Task CheckSignalStopLoss(CancellationToken stoppingToken)
{
    using var scope = serviceScopeFactory.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    await mediator.Send(new CheckSignalStopLossCommand(), stoppingToken);
}

// In ExecuteAsync, inside the while loop:
await CheckSignalEntry(stoppingToken);
await CheckSignalStopLoss(stoppingToken);
```

---

## Performance Considerations

- **`IX_SignalRecords_LastCheckedCandleAt`** (existing index) — the `ORDER BY LastCheckedCandleAt ASC TAKE 100` candidate query uses an index scan rather than a full table scan.
- **Pre-computed `stopLossPrices` dictionary** — computed once before the loop; O(1) lookup during the inner per-signal per-batch iteration.
- **Shared candle stream** — a single `GetKlines` call per batch serves all 100 candidates. Each signal filters independently via `OpenTime > signal.LastCheckedCandleAt`.
- **Early exit when `unhitCount == 0`** — stops fetching batches once every candidate is stopped out, eliminating unnecessary KuCoin API calls.
- **Batch ceiling ≤ 1500 candles** — `interval.GetEndDate(startAt)` computes the correct end boundary so each fetch covers exactly one window without over-fetching.

---

## Error Handling

| Scenario | Handling |
|---|---|
| No entered open signals | Early return after candidate query — no KuCoin call |
| Empty candle batch returned | Break inner loop; `lastCandleOpenTime` is null; no DB write |
| Signal not yet stopped out | `StopLoss` and `LastCheckedCandleAt` updated; `StopLossHitAt` remains null |
| Exception in loop (429 rate-limit, network, timeout) | Caught in `try/catch`; partial progress saved if ≥ 1 batch fetched; error logged via `ILogTrace`; no rethrow |
| DB `SaveChangesAsync` failure | Exception propagates to `FindSignalService` outer `catch` block and is logged there |

---

## Implementation Checklist

### Domain Layer
- [ ] Add `public int Leverage { get; set; } = 10;` to `SignalRecord`

### Infrastructure Layer
- [ ] Add `Leverage` column config in `SignalRecordConfiguration`:
  - `builder.Property(x => x.Leverage).HasDefaultValue(10);`
  - `builder.HasCheckConstraint("CK_SignalRecords_Leverage", "[Leverage] >= 1 AND [Leverage] <= 125");`
- [ ] Add migration: `AddSignalRecordLeverage`
- [ ] Confirm migration does not backfill — `HasDefaultValue(10)` handles existing rows via SQL Server column default

### Application Layer
- [ ] Create `CheckSignalStopLossCommand.cs` in `src/Cex/Cex.Application/Signal/Commands/CheckSignalStopLoss/`
  - `CheckSignalStopLossCommand` record implementing `IRequest`
  - `CheckSignalStopLossCommandHandler` with primary constructor injecting `IKuCoinService`, `IOptions<KuCoinConfig>`, `ILogTrace`, `ICexDbContext`
  - Full algorithm per Data Access section

### Hosted Service
- [ ] Add `CheckSignalStopLoss(CancellationToken)` private method to `FindSignalService`
- [ ] Call `await CheckSignalStopLoss(stoppingToken)` immediately after `await CheckSignalEntry(stoppingToken)` in `ExecuteAsync`
- [ ] Add `using Cex.Application.Signal.Commands.CheckSignalStopLoss;` import

### Testing
- [ ] Unit test: `stopLossPrice` formula — Long and Short at multiple leverage values (1, 10, 125)
- [ ] Unit test: hit detection boundary — Long (low exactly at / one tick above `stopLossPrice`), Short equivalent
- [ ] Unit test: `StopLoss` tracking stops at the hit candle and does not include subsequent candles
- [ ] Unit test: pointer monotonicity — `LastCheckedCandleAt` does not regress when `lastCandleOpenTime < existing LastCheckedCandleAt`
- [ ] Unit test: empty candidate list → `Handle` returns without calling `GetKlines`
- [ ] Unit test: exception mid-loop → `SaveChangesAsync` called if at least one batch was fetched

---

## Technical Notes

- **`GetKlines` sort contract** — the service returns candles sorted ascending by `OpenTime`. The inner loop relies on this for `TakeWhile(c => c.OpenTime <= hit.OpenTime)` to safely bound tracking to candles at or before the hit. The explicit `.OrderBy(c => c.OpenTime)` on `relevantCandles` is a defensive guard in case the upstream contract changes.

- **`StopLoss` seed gap** — `FindSignalCommandHandler` seeds `StopLoss` with a static divergence target: `entryPrice × 0.92` (Long) or `entryPrice × 1.08` (Short), an 8% offset. At 10× leverage, `stopLossPrice = 0.93 × entryPrice` (7% below entry). A hit candle whose `LowestPrice` falls between `0.93×` and `0.92×` entry (i.e., 7–8% adverse) triggers the stop but does not update `StopLoss`, because `Math.Min(0.92×entry, 0.925×entry) = 0.92×entry`. In that narrow range, `StopLoss` retains the original divergence seed rather than the true worst observed price. This is a known limitation of reusing the `StopLoss` field as both a divergence target and an adverse-price tracker.

- **`StopLoss` field repurposing** — `StopLoss` starts as a static divergence reference price and is overwritten once `CheckSignalStopLoss` processes the signal for the first time. The original divergence-based value is discarded. This is intentional — after entry, live worst-price proximity is operationally more useful.

- **`LastCheckedCandleAt` dual purpose** — for un-entered signals (`EntryHitAt IS NULL`) it is the `CheckSignalEntry` scan pointer; for entered signals (`EntryHitAt IS NOT NULL`) it is the stop-loss scan pointer anchored to `EntryHitAt.OpenTime` by `CheckSignalEntry`. Both commands share the same field by design; `CheckSignalStopLoss` picks up exactly where `CheckSignalEntry` left off.

- **`0.70m / s.Leverage` type safety** — `s.Leverage` is `int`; dividing a `decimal` literal by an `int` causes C# to promote the `int` to `decimal` before division. No integer truncation occurs.

- **First-run fetch volume** — the first time a signal is processed after entry, `LastCheckedCandleAt` equals `EntryHitAt.OpenTime`, which may be minutes or hours in the past. That run may fetch many candle batches. Subsequent runs fetch only ~1 minute of new candles.

  | Run | `LastCheckedCandleAt` (before run) | Candles fetched |
  |-----|------------------------------------|-----------------|
  | 1st (entry just hit) | `EntryHitAt.OpenTime` (set by CheckSignalEntry) | `EntryHitAt` → now (potentially large) |
  | 2nd | last candle from run 1 | ~1 min of new candles |
  | 3rd+ | always close to now | ~1 min of new candles |

- **Concurrent writes** — `FindSignalService` runs `CheckSignalEntry` and `CheckSignalStopLoss` sequentially on the same thread (no parallelism). A row cannot appear in both queries simultaneously: `CheckSignalEntry` filters `EntryHitAt IS NULL`; `CheckSignalStopLoss` filters `EntryHitAt IS NOT NULL`.

---

## Database Migration

```bash
dotnet ef migrations add AddSignalRecordLeverage \
  --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --startup-project src/WebAPI/WebAPI.csproj \
  --context CexDbContext
```

---

## Related Features

- **FindSignalCommand** — creates `SignalRecord` rows; sets initial `StopLoss` (later overwritten by this feature), `EntryPrice`, `TakeProfit`, and `SignalType`
- **CheckSignalEntry** — detects entry hits; anchors `LastCheckedCandleAt` to `EntryHitAt.OpenTime`, which is exactly where `CheckSignalStopLoss` begins scanning
- **CheckSignalTakeProfit** (future) — mirrors this feature for take-profit detection using `TakeProfitHitAt`

---

## Future

- `CheckSignalTakeProfitCommand` — same architecture; queries `EntryHitAt IS NOT NULL AND TakeProfitHitAt IS NULL`; tracks `TakeProfit` with the running best-observed price
- Configurable stop-loss margin percentage (currently hardcoded at 70%)
- Multi-symbol support (currently hardcoded to `BTCUSDT`)
- Notification on stop-loss hit via `INotifier` (same pattern as `FindSignalCommand`)
