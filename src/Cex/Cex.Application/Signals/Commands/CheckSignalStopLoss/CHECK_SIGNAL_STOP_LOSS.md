# CheckSignalStopLoss

> See [SIGNALS.md](../../SIGNALS.md) for the full entity schema, column reference, signal lifecycle, and stop-loss threshold formulas.

## Overview

`CheckSignalStopLossCommand` runs every minute inside `FindSignalService` and detects when entered positions have been stopped out. Queries up to 100 `Signal` rows where `EntryHitAt IS NOT NULL AND StopLossHitAt IS NULL`, fetches 1-minute BTCUSDT candles in batches starting from each signal's `LastCheckedCandleAt`, and marks `StopLossHitAt` when price crosses the stored `StopLoss` threshold.

**Module Location**: `src/Cex/Cex.Application/Signals/Commands/CheckSignalStopLoss/`
**Scope**: BTCUSDT only; all signal intervals

---

## Business Rules

**Hit condition** — stop-loss is triggered when a 1-minute candle's extreme crosses `signal.StopLoss`:

| Signal type | Hit condition |
|---|---|
| `Long` | `candle.LowestPrice <= signal.StopLoss` |
| `Short` | `candle.HighestPrice >= signal.StopLoss` |

**Pointer monotonicity** — `LastCheckedCandleAt` never moves backward. For unhit signals it is updated to `Max(LastCheckedCandleAt, lastCandleOpenTime)` at the end of each run.

---

## Algorithm

```
1. Query 100 Signals:
     WHERE EntryHitAt IS NOT NULL AND StopLossHitAt IS NULL
     ORDER BY LastCheckedCandleAt ASC
2. If empty -> return early (no KuCoin call)
3. startAt = Min(candidates, s => s.LastCheckedCandleAt)
4. Loop while startAt < now:
   a. Fetch <=1500 1-min candles from startAt to interval.GetEndDate(startAt)
   b. If batch empty -> break
   c. For each candidate where StopLossHitAt IS NULL:
        hit = FirstOrDefault(c =>
                  c.OpenTime > signal.LastCheckedCandleAt AND hit condition)
        If hit:
          StopLossHitAt           = hit.OpenTime
          StopLossHitAfterMinutes = (int)(hit.OpenTime - DetectedAt).TotalMinutes
          LastCheckedCandleAt     = hit.OpenTime
          unhitCount--
   d. startAt = batch[^1].OpenTime + 1 min
   e. If unhitCount == 0 -> break (all signals stopped out)
5. If no batch fetched -> return (no DB write)
6. For each unhit signal: LastCheckedCandleAt = Max(LastCheckedCandleAt, lastCandleOpenTime)
7. SaveChangesAsync
```

---

## Data Access

**Load candidates:**

```csharp
var candidates = await dbContext.Signals
    .Where(s => s.EntryHitAt != null && s.StopLossHitAt == null)
    .OrderBy(s => s.LastCheckedCandleAt)
    .Take(100)
    .ToListAsync(cancellationToken);
```

**Candle loop + hit detection:**

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

            signal.StopLossHitAt           = hit.OpenTime;
            signal.StopLossHitAfterMinutes = (int)(hit.OpenTime - signal.DetectedAt).TotalMinutes;
            signal.LastCheckedCandleAt     = hit.OpenTime;
            unhitCount--;
        }

        if (unhitCount == 0) break;
    }
}
catch (Exception ex)
{
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

**File**: `src/Cex/Cex.Application/Signals/Commands/CheckSignalStopLoss/CheckSignalStopLossCommand.cs`

```csharp
public record CheckSignalStopLossCommand : IRequest;

public class CheckSignalStopLossCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalStopLossCommand>
```

**Hosted Service** — called immediately after `CheckSignalEntry`, before `CheckSignalMaxProfit`:

```csharp
await CheckSignalEntry(stoppingToken);
await CheckSignalStopLoss(stoppingToken);
await CheckSignalMaxProfit(stoppingToken);
```

---

## Performance Considerations

- **`IX_Signals_LastCheckedCandleAt`** — the `ORDER BY LastCheckedCandleAt ASC TAKE 100` query uses this index rather than a full table scan.
- **Shared candle stream** — one `GetKlines` call per batch serves all 100 candidates; each signal filters independently in memory.
- **Early exit** — stops fetching batches once all candidates are stopped out.
- **No batch-level skip** — unlike `CheckSignalEntry`, no `batchHigh`/`batchLow` pre-check is applied per signal. Acceptable at current candidate counts; can be added if profiling shows it is needed.

---

## Error Handling

| Scenario | Handling |
|---|---|
| No entered open signals | Early return — no KuCoin call |
| First batch empty | Break; `lastCandleOpenTime` null; no DB write |
| Subsequent batch empty | Break; partial progress from prior batches saved |
| Signal not yet stopped out | `LastCheckedCandleAt` advanced; `StopLossHitAt` remains null |
| Exception in loop (429, network, timeout) | Caught; partial progress saved if >= 1 batch fetched; logged via `ILogTrace` |
| DB `SaveChangesAsync` failure | Propagates to `FindSignalService` catch block |

---

## Implementation Checklist

### Application Layer
- [x] `CheckSignalStopLossCommand.cs` — `CheckSignalStopLossCommand` record + `CheckSignalStopLossCommandHandler`

### Hosted Service
- [x] `CheckSignalStopLoss(CancellationToken)` private method in `FindSignalService`
- [x] Called after `CheckSignalEntry`, before `CheckSignalMaxProfit`

### Testing
- [ ] Hit detection boundary — Long (low exactly at / one tick above `StopLoss`), Short equivalent
- [ ] Pointer monotonicity — `LastCheckedCandleAt` does not regress
- [ ] Empty candidate list → `Handle` returns without calling `GetKlines`
- [ ] Exception mid-loop → `SaveChangesAsync` called if >= 1 batch was fetched

---

## Technical Notes

- **`GetKlines` sort contract** — candles are sorted ascending by `OpenTime`. `FirstOrDefault` relies on this to find the chronologically earliest hit. No defensive sort is applied.
- **`StopLoss` is a fixed threshold** — seeded by `FindSignalCommand`, never recomputed or overwritten by this command.
- **`LastCheckedCandleAt` dual purpose** — pre-entry: `CheckSignalEntry` scan pointer; post-entry: `CheckSignalStopLoss` scan pointer, anchored to `EntryHitAt.OpenTime`. This command picks up exactly where `CheckSignalEntry` left off.
- **Concurrent writes** — `CheckSignalEntry` (filters `EntryHitAt IS NULL`) and `CheckSignalStopLoss` (filters `EntryHitAt IS NOT NULL`) operate on disjoint row sets. No write conflict is possible.
- **First-run fetch volume** — on the first run after entry, `LastCheckedCandleAt` equals `EntryHitAt.OpenTime` which may be hours in the past. Subsequent runs fetch only ~1 minute of new candles.

---

## Related Features

- **FindSignalCommand** — seeds `StopLoss` threshold, `EntryPrice`, `SignalType`
- **CheckSignalEntry** — sets `EntryHitAt` and anchors `LastCheckedCandleAt` to `EntryHitAt.OpenTime`
- **CheckSignalMaxProfit** — reads `StopLossHitAt` as the scan upper bound for closed positions; must run after this command

---

## Future

- `CheckSignalTakeProfitCommand` — same architecture; queries `EntryHitAt IS NOT NULL AND TakeProfitHitAt IS NULL`; compares against `TakeProfit`
- Notification on stop-loss hit via `INotifier`
- Multi-symbol support
