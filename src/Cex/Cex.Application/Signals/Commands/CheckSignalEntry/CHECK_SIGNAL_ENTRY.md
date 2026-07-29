# CheckSignalEntry

> See [SIGNALS.md](../../SIGNALS.md) for the full entity schema, column reference, and signal lifecycle.

## Overview

`CheckSignalEntryCommand` runs every minute and detects when open signals (`EntryHitAt = NULL`) have had their entry price reached by 1-minute BTCUSDT candles. Processes up to 100 signals at a time (least-recently-checked first) and advances `LastCheckedCandleAt` per signal so each run only fetches new candles.

**Module Location**: `src/Cex/Cex.Application/Signals/Commands/CheckSignalEntry/`
**Scope**: BTCUSDT only; all signal intervals

---

## Algorithm

```
1. Query 100 Signals WHERE EntryHitAt IS NULL
   ORDER BY LastCheckedCandleAt ASC
2. If empty -> return early (no KuCoin call)
3. startAt = Min(candidates, s => s.LastCheckedCandleAt)
4. Loop while startAt < now:
   a. Fetch <=1500 1-min candles from startAt to interval.GetEndDate(startAt)
   b. If batch empty -> break
   c. batchHigh = Max(HighestPrice), batchLow = Min(LowestPrice)
   d. For each candidate where EntryHitAt IS NULL:
        - Skip if entry price is outside batch range (batch-level optimisation):
            Long  signal: EntryPrice < batchLow  -> skip
            Short signal: EntryPrice > batchHigh -> skip
        - Find earliest candle: OpenTime > LastCheckedCandleAt AND hit condition:
            Long  hit: LowestPrice  <= EntryPrice
            Short hit: HighestPrice >= EntryPrice
        - If hit:
            EntryHitAt             = hit.OpenTime
            EntryHitAfterMinutes   = (int)(hit.OpenTime - DetectedAt).TotalMinutes
            LastCheckedCandleAt    = hit.OpenTime  (anchor for CheckSignalStopLoss)
            MaxProfitCheckedAt     = hit.OpenTime  (anchor for CheckSignalMaxProfit)
            unhitCount--
   e. startAt = batch[^1].OpenTime + 1 min
   f. If unhitCount == 0 -> break early
5. If no batch was fetched -> return (no DB write)
6. For each unhit candidate: LastCheckedCandleAt = Max(LastCheckedCandleAt, lastCandleOpenTime)
   (pointer monotonicity — never move backward)
7. SaveChangesAsync
```

> **Why order by `LastCheckedCandleAt` ASC?** Backlogged signals are not starved by newly-created ones.

> **Why always advance `LastCheckedCandleAt` for unhit signals?** Even if no entry was hit, advancing the pointer ensures the next run only fetches ~1 new minute of candles instead of the full historical window again.

---

## Data Access

**Load candidates:**

```csharp
var candidates = await dbContext.Signals
    .Where(s => s.EntryHitAt == null)
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

        var batchHigh = batch.Max(c => c.HighestPrice);
        var batchLow  = batch.Min(c => c.LowestPrice);
        lastCandleOpenTime = batch[^1].OpenTime;
        startAt = lastCandleOpenTime.Value.AddMinutes(1);

        foreach (var signal in candidates.Where(s => s.EntryHitAt == null))
        {
            var inRange = signal.SignalType == SignalType.Long
                ? signal.EntryPrice >= batchLow
                : signal.EntryPrice <= batchHigh;

            if (!inRange) continue;

            var hit = batch.FirstOrDefault(c =>
                c.OpenTime > signal.LastCheckedCandleAt &&
                (signal.SignalType == SignalType.Long
                    ? c.LowestPrice  <= signal.EntryPrice
                    : c.HighestPrice >= signal.EntryPrice));

            if (hit is null) continue;

            signal.EntryHitAt            = hit.OpenTime;
            signal.EntryHitAfterMinutes  = (int)(hit.OpenTime - signal.DetectedAt).TotalMinutes;
            signal.LastCheckedCandleAt   = hit.OpenTime;
            signal.MaxProfitCheckedAt    = hit.OpenTime;
            unhitCount--;
        }

        if (unhitCount == 0) break;
    }
}
catch (Exception ex)
{
    logTrace.LogError("CheckSignalEntry loop interrupted — saving partial progress", ex);
}

if (lastCandleOpenTime is null) return;

foreach (var signal in candidates.Where(s => s.EntryHitAt == null && lastCandleOpenTime.Value > s.LastCheckedCandleAt))
    signal.LastCheckedCandleAt = lastCandleOpenTime.Value;

await dbContext.SaveChangesAsync(cancellationToken);
```

---

## Candle Fetch Window Over Time

| Run | `LastCheckedCandleAt` before run | Candles fetched |
|---|---|---|
| 1st (signal just created) | `CreatedAt` | `CreatedAt` → now (potentially large) |
| 2nd | Last candle from run 1 | ~1 min of new candles |
| 3rd+ | Close to now | ~1 min of new candles |

---

## Error Handling

| Scenario | Handling |
|---|---|
| No open signals | Early return — no KuCoin call |
| First batch empty | Break; `lastCandleOpenTime` null; no DB write |
| Entry price outside batch range | Signal skipped for hit detection; `LastCheckedCandleAt` still advanced |
| No candle hits entry | `EntryHitAt` stays null; `LastCheckedCandleAt` advanced |
| Exception in loop (429, network, timeout) | Caught; partial progress saved if >= 1 batch fetched; logged via `ILogTrace` |
| DB `SaveChangesAsync` failure | Propagates to `FindSignalService` catch block |

---

## Implementation Checklist

### Application Layer
- [x] `CheckSignalEntryCommand.cs` — `CheckSignalEntryCommand` record + `CheckSignalEntryCommandHandler`
  - Injects: `IKuCoinService`, `IOptions<KuCoinConfig>`, `ICexDbContext`, `ILogTrace`
  - Sets `EntryHitAfterMinutes`, `LastCheckedCandleAt`, `MaxProfitCheckedAt` on entry hit

### Infrastructure Layer
- [x] `IX_Signals_LastCheckedCandleAt` index on `LastCheckedCandleAt`
- [x] Migration: `AddSignalLastCheckedCandleAt`

### Hosted Service
- [x] `CheckSignalEntry(CancellationToken)` private method in `FindSignalService`
- [x] Called in `ExecuteAsync` before `CheckSignalStopLoss`

---

## Future

- `CheckSignalStopLossCommand` and `CheckSignalMaxProfitCommand` follow the same candle-loop pattern, picking up from `LastCheckedCandleAt` and `MaxProfitCheckedAt` respectively
