# CheckSignalEntry Feature

## Overview

`CheckSignalEntryCommand` runs every minute and checks whether any open signal records (those with `EntryHitAt = NULL`) have had their entry price reached by 1-minute candles. It processes signals in batches of 100 (least-recently-checked first) and tracks `LastCheckedCandleAt` per signal so each run only fetches new candles since the last check — not the entire historical window.

---

## Entity Change: Add `LastCheckedCandleAt` to `SignalRecord`

```
LastCheckedCandleAt  DateTime (datetime2(0))  Set to CreatedAt on insert; never null.
                                               Second-level precision — matches candle OpenTime granularity.
                                               OpenTime of the last 1-min candle checked for this signal.
                                               Updated after every check regardless of hit.
```

This is the key field that avoids re-scanning already-checked candles on subsequent runs.

---

## Algorithm

```
1. Query the 100 least-recently-checked SignalRecords where EntryHitAt IS NULL,
   ordered by LastCheckedCandleAt ASC
2. If no candidates → return early (no KuCoin call)
3. Find startAt = Min(candidates, s => s.LastCheckedCandleAt)
4. Loop while startAt < now:
   a. Fetch up to 1500 1-min candles (BTCUSDT) from startAt to interval.GetEndDate(startAt)
   b. If batch empty → break
   c. Compute batch price range: batchHigh = Max(HighestPrice), batchLow = Min(LowestPrice)
   d. For each candidate where EntryHitAt IS NULL:
        - Skip if entry is out of batch range:
            Long  signal: EntryPrice < batchLow  → skip
            Short signal: EntryPrice > batchHigh → skip
        - Find earliest candle in batch where:
            candle.OpenTime > signal.LastCheckedCandleAt
            Long  signal hit: candle.LowestPrice  <= signal.EntryPrice
            Short signal hit: candle.HighestPrice >= signal.EntryPrice
        - If hit found → set EntryHitAt = hit.OpenTime; decrement unhitCount
   e. Advance startAt = batch[^1].OpenTime + 1 min
   f. If unhitCount == 0 → break early
5. If no batches were fetched → return early (no DB write)
6. For every candidate: set LastCheckedCandleAt = Max(LastCheckedCandleAt, lastCandleOpenTime)
   (never move the pointer backwards in case of an early-exit gap)
7. SaveChangesAsync
```

> **Why order by `LastCheckedCandleAt` ASC?**
> This ensures the signals that were checked least recently come first, so backlogged signals are not starved by newly-created ones.

> **Why always update `LastCheckedCandleAt`?**
> Even if no entry was hit, we advance the pointer so the next run only fetches the 1 new minute of candles — not the entire window again.

---

## Data Access

**Step 1 — Load 100 least-recently-checked open signals:**

```csharp
var candidates = await dbContext.SignalRecords
    .Where(s => s.EntryHitAt == null)
    .OrderBy(s => s.LastCheckedCandleAt)
    .Take(100)
    .ToListAsync(cancellationToken);

if (candidates.Count == 0) return;
```

**Step 3-7 — Fetch batches and detect hits inline:**

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

            var checkFrom = signal.LastCheckedCandleAt;
            var hit = batch
                .FirstOrDefault(c => c.OpenTime > checkFrom &&
                                     (signal.SignalType == SignalType.Long
                                      ? c.LowestPrice  <= signal.EntryPrice
                                      : c.HighestPrice >= signal.EntryPrice));

            if (hit is not null)
            {
                signal.EntryHitAt = hit.OpenTime;
                unhitCount--;
            }
        }

        if (unhitCount == 0) break; // all hits found
    }
}
catch (Exception ex)
{
    // Any exception (rate-limit, network, timeout, etc.) during the loop:
    // fall through and save whatever progress was accumulated before the failure.
    // ILogTrace logs the error; we do not rethrow so partial progress is preserved.
    logTrace.Error(ex, "CheckSignalEntry loop interrupted — saving partial progress");
}

// Only write to DB if at least one entry was hit (unhitCount decreased from its initial value).
// LastCheckedCandleAt is advanced for all candidates to avoid re-scanning on the next run.
if (lastCandleOpenTime is null || unhitCount == candidates.Count) return;

foreach (var signal in candidates)
    if (lastCandleOpenTime.Value > signal.LastCheckedCandleAt)
        signal.LastCheckedCandleAt = lastCandleOpenTime.Value;
```

---

## Candle Fetch Window Over Time

| Run | `LastCheckedCandleAt` | Candles fetched |
| --- | --- | --- |
| 1st (signal just created) | initialized to `CreatedAt` on insert | `CreatedAt` → now (potentially large) |
| 2nd | set to last candle from run 1 | ~1 min of new candles |
| 3rd+ | always close to now | ~1 min of new candles |
| New signal enters top-100 | initialized to `CreatedAt` on insert | pulls window back to cover it; loop handles the gap |

---

## Implementation

### Domain Layer

- [ ] Add `LastCheckedCandleAt DateTime` to `SignalRecord` entity (non-nullable)

### Infrastructure Layer

- [ ] Add `LastCheckedCandleAt` to `SignalRecordConfiguration` with second-level precision: `HasPrecision(0).HasDefaultValueSql("GETUTCDATE()")` → maps to `datetime2(0)` in SQL Server
- [ ] Add index on `LastCheckedCandleAt` in `SignalRecordConfiguration` (supports the `ORDER BY LastCheckedCandleAt` query that runs every minute)
- [ ] Add migration: `AddSignalRecordLastCheckedCandleAt`

### Application Layer

**New file**: `src/Cex/Cex.Application/Indicator/Commands/CheckSignalEntry/CheckSignalEntryCommand.cs`

- Command: `CheckSignalEntryCommand` (no parameters — always operates on BTCUSDT 1min)
- Handler: `CheckSignalEntryCommandHandler`
  - Injects: `IKuCoinService`, `IOptions<KuCoinConfig>`, `ICexDbContext`, `ILogTrace`
  - Follows same primary constructor pattern as `FindSignalCommandHandler`

### Hosted Service

**Update**: `src/WebAPI/HostedServices/FindSignalService.cs`

Add a call to `CheckSignalEntryCommand` every minute inside `ExecuteAsync`:

```csharp
await mediator.Send(new CheckSignalEntryCommand(), stoppingToken);
```

This runs on every loop iteration (already delayed 60 seconds between loops).

---

## Error Handling

| Scenario | Handling |
| --- | --- |
| No open signals | Early return after step 1 — no KuCoin call made |
| No candles returned | Early return after loop — `lastCandleOpenTime` is null, no DB write |
| Signal out of price range | Skipped for hit detection; `LastCheckedCandleAt` still updated |
| No candle hits entry | `EntryHitAt` stays null; `LastCheckedCandleAt` still updated |
| Any exception in the loop (429, network, timeout, …) | Caught inside the handler; partial progress saved if at least one hit was found and at least one batch was fetched; error logged via `ILogTrace` |
| DB save failure | Exception propagates to `FindSignalService` catch block |

---

## Future

- `CheckSignalStopLossCommand` / `CheckSignalTakeProfitCommand` follow the same pattern, querying signals where `EntryHitAt IS NOT NULL` and `StopLossHitAt` / `TakeProfitHitAt IS NULL`. They can reuse the same `LastCheckedCandleAt` field or introduce their own pointer fields.
