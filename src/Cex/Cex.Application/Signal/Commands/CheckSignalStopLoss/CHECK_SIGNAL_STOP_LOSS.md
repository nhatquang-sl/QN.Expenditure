# CheckSignalStopLoss

## Overview

`CheckSignalStopLossCommand` runs every minute inside `FindSignalService` and detects when active positions have been stopped out. It queries up to 100 `SignalRecord` rows where `EntryHitAt IS NOT NULL AND StopLossHitAt IS NULL`, fetches 1-minute BTCUSDT candles in batches starting from each signal's `LastCheckedCandleAt` pointer (anchored to `EntryHitAt` by `CheckSignalEntry`), and marks `StopLossHitAt` when the market price crosses the stored `StopLoss` threshold set by `FindSignalCommandHandler`.

**Module Location**: `src/Cex/Cex.Application/Signal/Commands/CheckSignalStopLoss/`
**Scope**: BTCUSDT only; all signal intervals

---

## Data Model

### Entity: `SignalRecord` (Existing — no changes)

No new columns. The existing `StopLoss` field already holds the stop-loss threshold price seeded by `FindSignalCommandHandler`:
- Long: `entryPrice × 0.92` (8% below entry)
- Short: `entryPrice × 1.08` (8% above entry)

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
    public DateTime? EntryHitAt { get; set; }
    public DateTime? StopLossHitAt { get; set; }
    public DateTime? TakeProfitHitAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastCheckedCandleAt { get; set; }
}
```

### Business Rules

1. **Stop-loss threshold** — `StopLoss` is seeded by `FindSignalCommandHandler` and used directly as the hit threshold. No recomputation needed.
   - Long threshold: `StopLoss = entryPrice × 0.92` (8% below entry)
   - Short threshold: `StopLoss = entryPrice × 1.08` (8% above entry)

2. **Hit condition** — stop-loss is triggered when a 1-minute candle's extreme price crosses `signal.StopLoss`:
   - Long: `candle.LowestPrice ≤ signal.StopLoss`
   - Short: `candle.HighestPrice ≥ signal.StopLoss`

3. **Pointer monotonicity** — `LastCheckedCandleAt` must never move backward. For unhit signals it is updated to `Max(LastCheckedCandleAt, lastCandleOpenTime)` at the end of each run.

---

## Algorithm

```
1. Query 100 SignalRecords:
     WHERE EntryHitAt IS NOT NULL AND StopLossHitAt IS NULL
     ORDER BY LastCheckedCandleAt ASC
2. If candidates is empty → return early (no KuCoin call)
3. startAt           = Min(candidates, s => s.LastCheckedCandleAt)
   now               = DateTime.UtcNow
   unhitCount        = candidates.Count
   lastCandleOpenTime = null

4. while startAt < now:
   a. batch = GetKlines("BTCUSDT", 1min, startAt, interval.GetEndDate(startAt))
      (returns ≤ 1500 candles sorted ascending by OpenTime)
   b. if batch is empty → break
   c. lastCandleOpenTime = batch[^1].OpenTime
      startAt = lastCandleOpenTime + 1 min

   d. for each signal in candidates where StopLossHitAt is null:
        hit = batch.FirstOrDefault(c =>
                  c.OpenTime > signal.LastCheckedCandleAt &&
                  (Long  ? c.LowestPrice  <= signal.StopLoss
                         : c.HighestPrice >= signal.StopLoss))

        if hit is null → skip this signal for this batch

        signal.StopLossHitAt       = hit.OpenTime
        signal.LastCheckedCandleAt = hit.OpenTime
        unhitCount--

   e. if unhitCount == 0 → break (all signals stopped out)

5. if lastCandleOpenTime is null → return (no DB write — no batch was fetched)

6. For each signal in candidates WHERE StopLossHitAt IS NULL
                                 AND lastCandleOpenTime > signal.LastCheckedCandleAt:
     signal.LastCheckedCandleAt = lastCandleOpenTime

7. SaveChangesAsync
```

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

**Steps 3–7 — Candle loop, hit detection:**

```csharp
const IntervalType interval = IntervalType.OneMinute;
var startAt = candidates.Min(s => s.LastCheckedCandleAt);
var now = DateTime.UtcNow;
DateTime? lastCandleOpenTime = null;
var unhitCount = candidates.Count;

try
{
    while (startAt < now)
    {
        var batch = await kuCoinService.GetKlines(
            "BTCUSDT", interval, startAt, interval.GetEndDate(startAt), kuCoinConfig.Value);

        if (batch.Count == 0) break;

        lastCandleOpenTime = batch[^1].OpenTime;
        startAt = lastCandleOpenTime.Value.AddMinutes(1);

        foreach (var signal in candidates.Where(s => s.StopLossHitAt == null))
        {
            var hit = batch.FirstOrDefault(c =>
                c.OpenTime > signal.LastCheckedCandleAt &&
                (signal.SignalType == SignalType.Long
                    ? c.LowestPrice  <= signal.StopLoss
                    : c.HighestPrice >= signal.StopLoss));

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
- **Shared candle stream** — a single `GetKlines` call per batch serves all 100 candidates. Each signal filters independently via `OpenTime > signal.LastCheckedCandleAt`.
- **Early exit when `unhitCount == 0`** — stops fetching batches once every candidate is stopped out, eliminating unnecessary KuCoin API calls.
- **Batch ceiling ≤ 1500 candles** — `interval.GetEndDate(startAt)` computes the correct end boundary so each fetch covers exactly one window without over-fetching.
- **No batch-level skip optimization** — unlike `CheckSignalEntry` (which pre-computes `batchHigh`/`batchLow` and skips signals whose entry price is outside the batch range), `CheckSignalStopLoss` runs a full `FirstOrDefault` scan per signal per batch regardless of whether the batch price range can cross `signal.StopLoss`. This is acceptable given the expected low count of active candidates; the optimization can be added if profiling shows it is needed.

---

## Error Handling

| Scenario | Handling |
|---|---|
| No entered open signals | Early return after candidate query — no KuCoin call |
| First batch is empty | Break inner loop; `lastCandleOpenTime` is null; no DB write |
| Subsequent batch is empty | Break inner loop; partial progress from prior batches saved via `SaveChangesAsync` |
| Signal not yet stopped out | `LastCheckedCandleAt` updated; `StopLossHitAt` remains null |
| Exception in loop (429 rate-limit, network, timeout) | Caught in `try/catch`; partial progress saved if ≥ 1 batch fetched; error logged via `ILogTrace`; no rethrow |
| DB `SaveChangesAsync` failure | Exception propagates to `FindSignalService` outer `catch` block and is logged there |

---

## Implementation Checklist

### Application Layer
- [x] Create `CheckSignalStopLossCommand.cs` in `src/Cex/Cex.Application/Signal/Commands/CheckSignalStopLoss/`
  - `CheckSignalStopLossCommand` record implementing `IRequest`
  - `CheckSignalStopLossCommandHandler` with primary constructor injecting `IKuCoinService`, `IOptions<KuCoinConfig>`, `ILogTrace`, `ICexDbContext`
  - Full algorithm per Data Access section

### Hosted Service
- [x] Add `CheckSignalStopLoss(CancellationToken)` private method to `FindSignalService`
- [x] Call `await CheckSignalStopLoss(stoppingToken)` immediately after `await CheckSignalEntry(stoppingToken)` in `ExecuteAsync`
- [x] Add `using Cex.Application.Signal.Commands.CheckSignalStopLoss;` import

### Testing
- [ ] Unit test: hit detection boundary — Long (low exactly at / one tick above `StopLoss`), Short equivalent
- [ ] Unit test: pointer monotonicity — `LastCheckedCandleAt` does not regress when `lastCandleOpenTime < existing LastCheckedCandleAt`
- [ ] Unit test: empty candidate list → `Handle` returns without calling `GetKlines`
- [ ] Unit test: exception mid-loop → `SaveChangesAsync` called if at least one batch was fetched

---

## Technical Notes

- **`GetKlines` sort contract** — the service returns candles sorted ascending by `OpenTime`. The `FirstOrDefault` hit search relies on this order to find the **chronologically earliest** hit candle. If the contract is violated, `StopLossHitAt` would record a non-chronological candle's `OpenTime`, producing an incorrect timestamp. No defensive sort is applied; the contract is trusted.

- **`StopLoss` as fixed threshold** — `StopLoss` is seeded by `FindSignalCommandHandler` and treated as a constant for the lifetime of the position. `CheckSignalStopLoss` never overwrites it; it is read-only during stop-loss scanning.

- **`LastCheckedCandleAt` dual purpose** — for un-entered signals (`EntryHitAt IS NULL`) it is the `CheckSignalEntry` scan pointer; for entered signals (`EntryHitAt IS NOT NULL`) it is the stop-loss scan pointer anchored to `EntryHitAt.OpenTime` by `CheckSignalEntry`. Both commands share the same field by design; `CheckSignalStopLoss` picks up exactly where `CheckSignalEntry` left off.

- **First-run fetch volume** — the first time a signal is processed after entry, `LastCheckedCandleAt` equals `EntryHitAt.OpenTime`, which may be minutes or hours in the past. That run may fetch many candle batches. Subsequent runs fetch only ~1 minute of new candles.

  | Run | `LastCheckedCandleAt` (before run) | Candles fetched |
  |-----|------------------------------------|-----------------|
  | 1st (entry just hit) | `EntryHitAt.OpenTime` (set by CheckSignalEntry) | `EntryHitAt` → now (potentially large) |
  | 2nd | last candle from run 1 | ~1 min of new candles |
  | 3rd+ | always close to now | ~1 min of new candles |

- **Concurrent writes** — `FindSignalService` runs `CheckSignalEntry` and `CheckSignalStopLoss` sequentially on the same thread (no parallelism). A row cannot appear in both queries simultaneously: `CheckSignalEntry` filters `EntryHitAt IS NULL`; `CheckSignalStopLoss` filters `EntryHitAt IS NOT NULL`.

---

## Related Features

- **FindSignalCommand** — creates `SignalRecord` rows; sets `StopLoss` threshold, `EntryPrice`, `TakeProfit`, and `SignalType`
- **CheckSignalEntry** — detects entry hits; anchors `LastCheckedCandleAt` to `EntryHitAt.OpenTime`, which is exactly where `CheckSignalStopLoss` begins scanning
- **CheckSignalTakeProfit** (future) — mirrors this feature for take-profit detection using `TakeProfitHitAt`

---

## Future

- `CheckSignalTakeProfitCommand` — same architecture; queries `EntryHitAt IS NOT NULL AND TakeProfitHitAt IS NULL`; compares against stored `TakeProfit` threshold
- Multi-symbol support (currently hardcoded to `BTCUSDT`)
- Notification on stop-loss hit via `INotifier` (same pattern as `FindSignalCommand`)
