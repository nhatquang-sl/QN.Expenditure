# CheckSignalMaxProfit

> See [SIGNALS.md](../../SIGNALS.md) for the full entity schema, column reference, and signal lifecycle.
> Columns written by this command: `MaxProfit`, `MaxProfitHitAt`, `MaxProfitHitAfterMinutes`, `MaxProfitCheckedAt`.

## Overview

`CheckSignalMaxProfitCommand` runs every minute inside `FindSignalService` and tracks the peak leverage-adjusted profit achieved by each entered position. For stopped-out positions it scans the closed window (entry → stop-loss) once, then stops. For open positions it scans incrementally to the current minute on every run.

**Module Location**: `src/Cex/Cex.Application/Signals/Commands/CheckSignalMaxProfit/`
**Scope**: BTCUSDT only; all signal intervals

---

## Business Rules

1. **Profit formula** per candle (leverage-adjusted):
   - Long: `profitPct = (candle.HighestPrice - entryPrice) / entryPrice * 100 * Leverage`
   - Short: `profitPct = (entryPrice - candle.LowestPrice) / entryPrice * 100 * Leverage`

2. **Max update condition** — `MaxProfit` and `MaxProfitHitAt` are overwritten only when `profitPct > signal.MaxProfit`. Strict greater-than preserves the earliest candle on equal values.

3. **"Never in profit" state** — if no candle produces `profitPct > 0`, `MaxProfitHitAt` remains `NULL` and `MaxProfit` stays `0` even after the scan is complete.

4. **Scan window**:
   - Open position (`StopLossHitAt IS NULL`): scans from `MaxProfitCheckedAt ?? EntryHitAt` → now; re-runs indefinitely every minute.
   - Stopped-out (`StopLossHitAt IS NOT NULL`): scans `MaxProfitCheckedAt ?? EntryHitAt` → `StopLossHitAt`; signal drops from the query once `MaxProfitCheckedAt >= StopLossHitAt`.

5. **Pointer initialisation** — `MaxProfitCheckedAt` is `NULL` at record creation. Set to `hit.OpenTime` by `CheckSignalEntry` on entry detection. If still `NULL` when this command runs (e.g. backfilled rows), all pointer reads fall back to `EntryHitAt` via null-coalescing (`?? EntryHitAt`).

6. **Pointer monotonicity** — `MaxProfitCheckedAt` never moves backward.

7. **Leverage** — `int`, range 1–125, default 10. Multiplying `decimal × int` promotes the `int` to `decimal` — no truncation.

---

## Algorithm

```
1. Query 100 Signals:
     WHERE EntryHitAt IS NOT NULL
       AND (StopLossHitAt IS NULL
            OR (MaxProfitCheckedAt ?? EntryHitAt) < StopLossHitAt)
     ORDER BY (MaxProfitCheckedAt ?? EntryHitAt) ASC

2. If empty -> return early

3. startAt = Min(candidates, s => s.MaxProfitCheckedAt ?? s.EntryHitAt)

4. Loop while startAt < now:
   a. Fetch <=1500 1-min candles from startAt to interval.GetEndDate(startAt)
   b. If batch empty -> break
   c. For each signal in candidates:
        checkFrom      = MaxProfitCheckedAt ?? EntryHitAt
        scanEnd        = StopLossHitAt (null = no upper bound)
        relevantCandles = batch
            .Where(c => c.OpenTime > checkFrom AND (scanEnd == null OR c.OpenTime <= scanEnd))
        If relevantCandles empty -> skip this signal for this batch

        For each candle in relevantCandles (ascending OpenTime):
          Compute profitPct per formula above
          If profitPct > MaxProfit:
            MaxProfit               = profitPct
            MaxProfitHitAt          = c.OpenTime
            MaxProfitHitAfterMinutes = (int)(c.OpenTime - DetectedAt).TotalMinutes

        newPointer = relevantCandles[^1].OpenTime  (latest in range, <= scanEnd)
        If newPointer > (MaxProfitCheckedAt ?? EntryHitAt):
          MaxProfitCheckedAt = newPointer

   d. startAt = batch[^1].OpenTime + 1 min

5. If no batch fetched -> return (no DB write)
6. SaveChangesAsync
```

> **Why a per-signal pointer rather than a shared `lastBatchOpenTime`?**
> Open and stopped-out signals have different effective scan ends. A shared pointer would advance stopped-out signals past their `StopLossHitAt`, incorrectly including post-close candles. The per-signal pointer caps naturally at `StopLossHitAt` via the `relevantCandles` filter.

> **Why no `unhitCount`-style early break?**
> Open positions are never "done" within a run; a break condition would require tracking open vs stopped-out counts for minimal gain.

---

## Data Access

**Load candidates:**

```csharp
var candidates = await dbContext.Signals
    .Where(s => s.EntryHitAt != null &&
                (s.StopLossHitAt == null ||
                 (s.MaxProfitCheckedAt ?? s.EntryHitAt) < s.StopLossHitAt))
    .OrderBy(s => s.MaxProfitCheckedAt ?? s.EntryHitAt)
    .Take(100)
    .ToListAsync(cancellationToken);
```

**Candle loop + profit tracking:**

```csharp
const IntervalType interval = IntervalType.OneMinute;
var startAt = candidates.Min(s => s.MaxProfitCheckedAt ?? s.EntryHitAt!.Value);
var now = DateTime.UtcNow;
DateTime? lastBatchOpenTime = null;

try
{
    while (startAt < now)
    {
        var batch = await kuCoinService.GetKlines(
            "BTCUSDT", interval, startAt, interval.GetEndDate(startAt), kuCoinConfig.Value);

        if (batch.Count == 0) break;

        lastBatchOpenTime = batch[^1].OpenTime;
        startAt = lastBatchOpenTime.Value.AddMinutes(1);

        foreach (var signal in candidates)
        {
            var scanEnd = signal.StopLossHitAt;
            var relevantCandles = batch
                .Where(c => c.OpenTime > (signal.MaxProfitCheckedAt ?? signal.EntryHitAt!.Value) &&
                            (scanEnd == null || c.OpenTime <= scanEnd))
                .ToList();

            if (relevantCandles.Count == 0) continue;

            foreach (var c in relevantCandles)
            {
                var profitPct = signal.SignalType == SignalType.Long
                    ? (c.HighestPrice - signal.EntryPrice) / signal.EntryPrice * 100m * signal.Leverage
                    : (signal.EntryPrice - c.LowestPrice)  / signal.EntryPrice * 100m * signal.Leverage;

                if (profitPct > signal.MaxProfit)
                {
                    signal.MaxProfit                = profitPct;
                    signal.MaxProfitHitAt           = c.OpenTime;
                    signal.MaxProfitHitAfterMinutes = (int)(c.OpenTime - signal.DetectedAt).TotalMinutes;
                }
            }

            var newPointer = relevantCandles[^1].OpenTime;
            if (newPointer > (signal.MaxProfitCheckedAt ?? signal.EntryHitAt!.Value))
                signal.MaxProfitCheckedAt = newPointer;
        }
    }
}
catch (Exception ex)
{
    logTrace.LogError("CheckSignalMaxProfit loop interrupted — saving partial progress", ex);
}

if (lastBatchOpenTime is null) return;

await dbContext.SaveChangesAsync(cancellationToken);
```

---

## Backend Architecture

**File**: `src/Cex/Cex.Application/Signals/Commands/CheckSignalMaxProfit/CheckSignalMaxProfitCommand.cs`

```csharp
public record CheckSignalMaxProfitCommand : IRequest;

public class CheckSignalMaxProfitCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalMaxProfitCommand>
```

**Hosted Service** — called last in the cycle, after `CheckSignalStopLoss`:

```csharp
await CheckSignalEntry(stoppingToken);
await CheckSignalStopLoss(stoppingToken);
await CheckSignalMaxProfit(stoppingToken);
```

`CheckSignalStopLoss` **must run before** this command each cycle — see Technical Notes.

---

## Performance Considerations

- **`IX_Signals_MaxProfitCheckedAt`** — supports `ORDER BY MaxProfitCheckedAt ASC TAKE 100`.
- **Shared candle stream** — one `GetKlines` call per batch serves all 100 candidates.
- **Stopped-out signals terminate naturally** — once `MaxProfitCheckedAt >= StopLossHitAt` the signal drops from the query permanently.
- **No-op saves** — if all candidates' `relevantCandles` are empty (all pointers up to date), no entity is dirtied; EF Core change tracking avoids issuing SQL UPDATEs despite `SaveChangesAsync` being called.

---

## Error Handling

| Scenario | Handling |
|---|---|
| No entered unscanned signals | Early return — no KuCoin call |
| First batch empty | Break; `lastBatchOpenTime` null; no DB write |
| Signal's `relevantCandles` empty for a batch | Skip signal for that batch; `MaxProfitCheckedAt` unchanged |
| Exception in loop (429, network, timeout) | Caught; partial progress saved if >= 1 batch fetched; logged via `ILogTrace` |
| DB `SaveChangesAsync` failure | Propagates to `FindSignalService` catch block |

---

## Implementation Checklist

### Domain Layer
- [x] `Leverage`, `MaxProfit`, `MaxProfitHitAt`, `MaxProfitCheckedAt` added to `Signal`

### Infrastructure Layer
- [x] EF Core config for the four new columns in `SignalConfiguration`
- [x] `IX_Signals_MaxProfitCheckedAt` index
- [x] Migration: `AddSignalMaxProfit` with backfill:
  `UPDATE Signals SET MaxProfitCheckedAt = EntryHitAt WHERE EntryHitAt IS NOT NULL`

### Application Layer
- [x] `FindSignalCommand.cs`: does NOT set `MaxProfitCheckedAt` — left null at creation
- [x] `CheckSignalEntryCommand.cs`: sets `signal.MaxProfitCheckedAt = hit.OpenTime` on entry hit
- [x] `CheckSignalMaxProfitCommand.cs` — full algorithm implemented

### Hosted Service
- [x] `CheckSignalMaxProfit(CancellationToken)` private method in `FindSignalService`
- [x] Called after `CheckSignalStopLoss`

### Testing
- [ ] Profit formula — Long and Short at leverage 1, 10, 125
- [ ] `MaxProfit` updated only on strict greater-than (tie retains earlier candle)
- [ ] `MaxProfitHitAt` remains null when no candle produces `profitPct > 0`
- [ ] Open signal `MaxProfitCheckedAt` advances to last batch candle per run
- [ ] Stopped-out signal `MaxProfitCheckedAt` caps at `StopLossHitAt`; excluded from query next run
- [ ] Empty candidate list → returns without calling `GetKlines`
- [ ] Exception mid-loop → `SaveChangesAsync` called if >= 1 batch fetched

---

## Technical Notes

- **`CheckSignalStopLoss` must run before `CheckSignalMaxProfit` each cycle** — if this command ran first, an open signal's `MaxProfitCheckedAt` could advance past the stop-loss candle before `StopLossHitAt` is set. Running `CheckSignalStopLoss` first ensures `StopLossHitAt` is current when the `scanEnd` cap is applied.
- **`StopLossHitAt` is always a real candle `OpenTime`** — set by `CheckSignalStopLoss` to `hit.OpenTime` of an actual KuCoin candle. This guarantees a candle with exactly that `OpenTime` exists in future batches, allowing `MaxProfitCheckedAt` to reach `StopLossHitAt` precisely.
- **`GetKlines` sort contract** — `relevantCandles` inherits ascending `OpenTime` order from `GetKlines`. `relevantCandles[^1]` is the chronologically latest candle. No defensive sort is applied; the contract is trusted.
- **First-run fetch volume** — on the first run, `MaxProfitCheckedAt` equals `EntryHitAt`, which may be hours in the past. Subsequent runs fetch ~1 minute of new candles.

---

## Related Features

- **FindSignalCommand** — seeds `EntryPrice`, `SignalType`, `Leverage`; `MaxProfitCheckedAt` left null
- **CheckSignalEntry** — sets `EntryHitAt` and initialises `MaxProfitCheckedAt = hit.OpenTime`
- **CheckSignalStopLoss** — sets `StopLossHitAt` (the scan upper bound for closed positions); must run before this command

---

## Future

- Expose `MaxProfit` and `MaxProfitHitAt` in a dedicated analytics endpoint
- Configurable leverage per signal at creation time
- Early-exit optimisation: break the candle loop when all stopped-out candidates are complete and no open candidates remain
- Notification on new all-time high profit via `INotifier`
