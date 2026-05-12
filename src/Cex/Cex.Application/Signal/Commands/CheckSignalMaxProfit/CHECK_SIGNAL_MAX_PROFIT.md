# CheckSignalMaxProfit

## Overview

`CheckSignalMaxProfitCommand` runs every minute inside `FindSignalService` and tracks the peak leverage-adjusted profit achieved by each entered position. Two scan windows apply: for stopped-out positions it scans the closed window from entry to stop-loss (once, then complete); for open positions it scans incrementally from the last checkpoint to the current minute on every run. The result — `MaxProfit` (%) and `MaxProfitHitAt` (candle timestamp) — provides retrospective and live insight into how far in-profit a position reached before or since closing.

**Module Location**: `src/Cex/Cex.Application/Signal/Commands/CheckSignalMaxProfit/`
**Scope**: BTCUSDT only; all signal intervals

---

## Data Model

### Entity: `Signal` (Existing — modified)

Four new columns:

| Column | Type | Nullable | Default | Constraints | Notes |
|---|---|---|---|---|---|
| Leverage | int | No | 10 | CK: `Leverage >= 1 AND Leverage <= 125` | Scales raw price movement to leveraged profit % |
| MaxProfit | decimal(10,4) | No | 0 | — | Best leverage-adjusted profit % seen; 0 = never above entry |
| MaxProfitHitAt | datetime2(0) | Yes | NULL | — | OpenTime of the candle where best price was observed; null if `MaxProfit` was never updated above 0 |
| MaxProfitCheckedAt | datetime2(0) | Yes | NULL | — | Scan pointer; null until entry is detected; set to `EntryHitAt` by `CheckSignalEntry` when entry is detected; advanced by `CheckSignalMaxProfit` each run thereafter |

Updated entity snippet:

```csharp
public class Signal
{
    // ... existing fields ...
    public int Leverage { get; set; } = 10;
    public decimal MaxProfit { get; set; } = 0;
    public DateTime? MaxProfitHitAt { get; set; }
    public DateTime? MaxProfitCheckedAt { get; set; }
}
```

### Business Rules

1. **Profit formula** per candle (leverage-adjusted):
   - Long: `profitPct = (candle.HighestPrice − entryPrice) / entryPrice × 100 × Leverage`
   - Short: `profitPct = (entryPrice − candle.LowestPrice) / entryPrice × 100 × Leverage`

2. **Max update condition** — `MaxProfit` and `MaxProfitHitAt` are overwritten only when `profitPct > signal.MaxProfit`. Strict greater-than preserves the earliest candle when two candles produce identical profit values.

3. **`MaxProfitHitAt` remains null when `MaxProfit` stays at 0** — if no candle in the entire scan window produces `profitPct > 0` (price never moved above entry for Long / below entry for Short), `MaxProfitHitAt` is never set and remains null even after the scan is fully complete. `MaxProfit = 0` with `MaxProfitHitAt = null` means the position was never in profit.

4. **Scan window**:
   - Open position (`StopLossHitAt IS NULL`): scans from `MaxProfitCheckedAt` → now; re-runs every minute indefinitely.
   - Stopped-out position (`StopLossHitAt IS NOT NULL`): scans from `MaxProfitCheckedAt` → `StopLossHitAt`; complete when `MaxProfitCheckedAt >= StopLossHitAt`.

5. **Pointer initialisation** — `MaxProfitCheckedAt` is `null` at record creation and remains `null` until entry is detected. `CheckSignalEntry` sets it to `hit.OpenTime` (= `EntryHitAt`) when entry is detected, anchoring the max-profit scan to the same point as the stop-loss scan.

6. **Pointer monotonicity** — `MaxProfitCheckedAt` never moves backward.

7. **Leverage range** — 1–125; enforced by a DB check constraint.

8. **Leverage default** — existing rows without an explicit value default to 10.

---

## Algorithm

```
1. Query 100 Signals:
     WHERE MaxProfitCheckedAt IS NOT NULL
       AND (StopLossHitAt IS NULL
            OR MaxProfitCheckedAt < StopLossHitAt)
     ORDER BY MaxProfitCheckedAt ASC

2. If candidates empty → return early

3. startAt = Min(candidates, s => s.MaxProfitCheckedAt)
   now     = DateTime.UtcNow
   lastBatchOpenTime = null

4. while startAt < now:
   a. batch = GetKlines("BTCUSDT", 1min, startAt, interval.GetEndDate(startAt))
      (returns ≤ 1500 candles sorted ascending by OpenTime)
   b. if batch empty → break
   c. lastBatchOpenTime = batch[^1].OpenTime
      startAt           = lastBatchOpenTime + 1 min

   d. for each signal in candidates:
        checkFrom = signal.MaxProfitCheckedAt
        scanEnd   = signal.StopLossHitAt    (null → no upper bound)

        relevantCandles = batch
            .Where(c => c.OpenTime > checkFrom
                     && (scanEnd == null || c.OpenTime <= scanEnd))
            .ToList()
            (inherits ascending OpenTime order from GetKlines)

        if relevantCandles empty → skip this signal for this batch

        for each c in relevantCandles (ascending OpenTime):
          profitPct = Long  ? (c.HighestPrice − entryPrice) / entryPrice × 100 × Leverage
                      Short ? (entryPrice − c.LowestPrice)  / entryPrice × 100 × Leverage
          if profitPct > signal.MaxProfit:
            signal.MaxProfit      = profitPct
            signal.MaxProfitHitAt = c.OpenTime

        newPointer = relevantCandles[^1].OpenTime
                     ← last element is chronologically latest because list is sorted ascending;
                       always ≤ scanEnd due to the Where filter
        if newPointer > signal.MaxProfitCheckedAt!.Value:
          signal.MaxProfitCheckedAt = newPointer

5. if lastBatchOpenTime is null → return (no DB write — no batch fetched)

6. SaveChangesAsync
```

> **Why a per-signal pointer rather than the shared `lastBatchOpenTime` approach used by `CheckSignalStopLoss`?**
> Open and stopped-out signals have different effective scan ends. A shared pointer would advance stopped-out signals past their `StopLossHitAt`, including post-close candles in the profit calculation. Per-signal advance caps naturally at `StopLossHitAt` via the `relevantCandles` filter.

> **Why no early `unhitCount`-style break?**
> Open positions are never "done" within a run — they always scan to `now`. A break condition would require tracking open vs stopped-out counts for minimal gain. The `while startAt < now` condition terminates naturally.

---

## Data Access

**Step 1 — Load candidates:**

```csharp
var candidates = await dbContext.Signals
    .Where(s => s.MaxProfitCheckedAt != null &&
                (s.StopLossHitAt == null ||
                 s.MaxProfitCheckedAt < s.StopLossHitAt))
    .OrderBy(s => s.MaxProfitCheckedAt)
    .Take(100)
    .ToListAsync(cancellationToken);

if (candidates.Count == 0) return;
```

**Steps 3–6 — Candle loop, profit tracking:**

```csharp
const IntervalType interval = IntervalType.OneMinute;
var startAt = candidates.Min(s => s.MaxProfitCheckedAt!.Value);
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

            // GetKlines returns candles sorted ascending by OpenTime.
            // relevantCandles inherits that order; no defensive sort is applied.
            var relevantCandles = batch
                .Where(c => c.OpenTime > signal.MaxProfitCheckedAt!.Value &&
                            (scanEnd == null || c.OpenTime <= scanEnd))
                .ToList();

            if (relevantCandles.Count == 0) continue;

            foreach (var c in relevantCandles)
            {
                // signal.Leverage is int; multiplying decimal by int promotes int to decimal — no truncation.
                var profitPct = signal.SignalType == SignalType.Long
                    ? (c.HighestPrice - signal.EntryPrice) / signal.EntryPrice * 100m * signal.Leverage
                    : (signal.EntryPrice - c.LowestPrice)  / signal.EntryPrice * 100m * signal.Leverage;

                if (profitPct > signal.MaxProfit)
                {
                    signal.MaxProfit      = profitPct;
                    signal.MaxProfitHitAt = c.OpenTime;
                }
            }

            // relevantCandles is sorted ascending; [^1] is the latest candle in the range.
            // Its OpenTime is always ≤ scanEnd because of the Where filter above.
            var newPointer = relevantCandles[^1].OpenTime;
            if (newPointer > signal.MaxProfitCheckedAt)
                signal.MaxProfitCheckedAt = newPointer;
        }
    }
}
catch (Exception ex)
{
    // Any exception (rate-limit 429, network, timeout) during the loop:
    // fall through and save whatever progress was accumulated before the failure.
    logTrace.LogError("CheckSignalMaxProfit loop interrupted — saving partial progress", ex);
}

if (lastBatchOpenTime is null) return;

await dbContext.SaveChangesAsync(cancellationToken);
```

---

## Backend Architecture

### Domain Layer

**Modified:** `src/Cex/Cex.Domain/Entities/Signal.cs`
- Add `public int Leverage { get; set; } = 10;`
- Add `public decimal MaxProfit { get; set; } = 0;`
- Add `public DateTime? MaxProfitHitAt { get; set; }`
- Add `public DateTime? MaxProfitCheckedAt { get; set; }`

### Infrastructure Layer

**Modified:** `src/Cex/Cex.Infrastructure/Data/Configurations/SignalConfiguration.cs`

```csharp
builder.Property(x => x.Leverage)
    .HasDefaultValue(10);
builder.HasCheckConstraint("CK_Signals_Leverage", "[Leverage] >= 1 AND [Leverage] <= 125");

builder.Property(x => x.MaxProfit)
    .HasPrecision(10, 4)
    .HasDefaultValue(0m);

builder.Property(x => x.MaxProfitHitAt).HasPrecision(0);
builder.Property(x => x.MaxProfitCheckedAt).HasPrecision(0);

builder.HasIndex(x => x.MaxProfitCheckedAt);
```

**Migration name:** `AddSignalMaxProfit`

### Application Layer

**Modified:** `src/Cex/Cex.Application/Signal/Commands/FindSignalCommand.cs`

`MaxProfitCheckedAt` is **not set** at record creation — it remains `null` until entry is detected.

**Modified:** `src/Cex/Cex.Application/Signal/Commands/CheckSignalEntry/CheckSignalEntryCommand.cs`

In the entry-hit block, add `MaxProfitCheckedAt` alongside the existing `LastCheckedCandleAt` assignment:

```csharp
signal.EntryHitAt           = hit.OpenTime;
signal.LastCheckedCandleAt  = hit.OpenTime; // anchor for stop-loss check
signal.MaxProfitCheckedAt   = hit.OpenTime; // anchor for max-profit check
```

**New file:** `src/Cex/Cex.Application/Signal/Commands/CheckSignalMaxProfit/CheckSignalMaxProfitCommand.cs`

```csharp
using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Signals.Commands.CheckSignalMaxProfit;

public record CheckSignalMaxProfitCommand : IRequest;

public class CheckSignalMaxProfitCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<CheckSignalMaxProfitCommand>
{
    public async Task Handle(CheckSignalMaxProfitCommand command, CancellationToken cancellationToken)
    {
        // ... (see Data Access section)
    }
}
```

### Hosted Service

**Modified:** `src/WebAPI/HostedServices/FindSignalService.cs`

Add `CheckSignalMaxProfit` private method and call it **after** `CheckSignalStopLoss` — this ordering is a correctness invariant (see Technical Notes):

```csharp
private async Task CheckSignalMaxProfit(CancellationToken stoppingToken)
{
    using var scope = serviceScopeFactory.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    await mediator.Send(new CheckSignalMaxProfitCommand(), stoppingToken);
}

// In ExecuteAsync, inside the trigger block:
await CheckSignalEntry(stoppingToken);
await CheckSignalStopLoss(stoppingToken);
await CheckSignalMaxProfit(stoppingToken);
```

---

## Performance Considerations

- **`IX_Signals_MaxProfitCheckedAt`** (new index) — supports `ORDER BY MaxProfitCheckedAt ASC TAKE 100`.
- **Shared candle stream** — one `GetKlines` call per batch serves all 100 candidates; per-signal filtering is in-memory.
- **No batch-level skip optimisation** — no `batchHigh`/`batchLow` pre-check is applied. Acceptable given the low expected count of active max-profit candidates; can be added if profiling shows it is needed.
- **Stopped-out signals terminate naturally** — once `MaxProfitCheckedAt = StopLossHitAt` the signal drops from the query; no extra API calls for completed scans.
- **No-op saves** — if all candidates' `relevantCandles` are empty for every batch (all pointers already up to date), no entity is dirtied but `SaveChangesAsync` is still called since `lastBatchOpenTime` is non-null. EF Core change tracking avoids issuing any SQL UPDATE in this case.

---

## Error Handling

| Scenario | Handling |
|---|---|
| No entered open/unscanned signals | Early return after candidate query — no KuCoin call |
| First batch is empty | Break; `lastBatchOpenTime` is null; no DB write |
| Subsequent batch is empty | Break; partial progress from prior batches saved via `SaveChangesAsync` |
| Signal's `relevantCandles` empty for this batch | Skip signal for this batch; `MaxProfitCheckedAt` unchanged |
| Exception in loop (429, network, timeout) | Caught in `try/catch`; partial progress saved if ≥ 1 batch fetched; error logged via `ILogTrace`; no rethrow |
| DB `SaveChangesAsync` failure | Exception propagates to `FindSignalService` outer `catch` block and is logged there |

---

## Implementation Checklist

### Domain Layer
- [x] Add `Leverage`, `MaxProfit`, `MaxProfitHitAt`, `MaxProfitCheckedAt` to `Signal`

### Infrastructure Layer
- [x] Add `Leverage`, `MaxProfit`, `MaxProfitHitAt`, `MaxProfitCheckedAt` config in `SignalConfiguration`
- [x] Add `IX_Signals_MaxProfitCheckedAt` index
- [x] Add migration: `AddSignalMaxProfit`

### Application Layer
- [x] Modify `FindSignalCommand.cs`: do NOT set `MaxProfitCheckedAt` — leave it `null` at record creation
- [x] Modify `CheckSignalEntryCommand.cs`: add `signal.MaxProfitCheckedAt = hit.OpenTime;` in the entry-hit block
- [x] Create `CheckSignalMaxProfitCommand.cs` with handler (full algorithm per Data Access section)

### Hosted Service
- [x] Add `CheckSignalMaxProfit(CancellationToken)` private method to `FindSignalService`
- [x] Call `await CheckSignalMaxProfit(stoppingToken)` immediately **after** `await CheckSignalStopLoss(stoppingToken)`
- [x] Add `using Cex.Application.Signals.Commands.CheckSignalMaxProfit;` import

### Testing
- [ ] Unit: profit formula — Long and Short at leverage values 1, 10, 125
- [ ] Unit: `MaxProfit` updated only when `profitPct > existing MaxProfit` (strict greater-than; tie retains earlier candle)
- [ ] Unit: `MaxProfitHitAt` remains null when no candle produces `profitPct > 0`
- [ ] Unit: open signal `MaxProfitCheckedAt` advances to last batch candle per run
- [ ] Unit: stopped-out signal `MaxProfitCheckedAt` caps at `StopLossHitAt`; signal excluded from query next run
- [ ] Unit: empty candidate list → `Handle` returns without calling `GetKlines`
- [ ] Unit: exception mid-loop → `SaveChangesAsync` called if ≥ 1 batch was fetched

---

## Technical Notes

- **`CheckSignalStopLoss` must run before `CheckSignalMaxProfit` each cycle** — if `CheckSignalMaxProfit` ran first, an open signal's `MaxProfitCheckedAt` could advance past the stop-loss candle before `StopLossHitAt` is set. In the next run, `MaxProfitCheckedAt >= StopLossHitAt` would exclude the signal from the query while `MaxProfit` already includes candles after the stop. Running `CheckSignalStopLoss` first ensures `StopLossHitAt` is current when `CheckSignalMaxProfit` applies the `scanEnd` cap.

- **`GetKlines` sort contract** — `relevantCandles` inherits ascending `OpenTime` order from `GetKlines`. `relevantCandles[^1]` is the chronologically latest candle in the range; this is required for correct pointer advance. No defensive sort is applied; the contract is trusted.

- **`StopLossHitAt` is always a real candle `OpenTime`** — `CheckSignalStopLoss` sets it to `hit.OpenTime` of an actual KuCoin candle. This guarantees a candle with exactly that `OpenTime` exists in future batches, allowing `MaxProfitCheckedAt` to reach `StopLossHitAt` precisely so the signal drops out of the query.

- **`MaxProfit = 0` with `MaxProfitHitAt = null` after full scan** — a position where price moved immediately and entirely against entry completes its scan with `MaxProfit = 0` and `MaxProfitHitAt = null`. This is the expected "never in profit" sentinel state.

- **`Leverage` type safety** — `signal.Leverage` is `int`; multiplying a `decimal` expression by an `int` promotes the `int` to `decimal` — no integer truncation.

- **`MaxProfitCheckedAt` lifecycle** — `null` at record creation; set to `hit.OpenTime` (= `EntryHitAt`) by `CheckSignalEntry` when entry is detected; advanced by `CheckSignalMaxProfit` each run thereafter. A `null` value means entry has not yet been hit.

- **First-run fetch volume** — on the first run for a signal, `MaxProfitCheckedAt` equals `EntryHitAt` (just set by `CheckSignalEntry`), which may be hours or days in the past. That run fetches many candle batches; subsequent runs fetch only ~1 minute of new candles.

  | Run | `MaxProfitCheckedAt` (before run) | Candles fetched |
  |-----|-----------------------------------|-----------------|
  | 1st | `EntryHitAt` (set by `CheckSignalEntry`) | `EntryHitAt` → now (potentially large) |
  | 2nd | last candle from run 1 | ~1 min of new candles |
  | 3rd+ | close to now | ~1 min of new candles |

---

## Database Migration

```bash
dotnet ef migrations add AddSignalMaxProfit \
  --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --startup-project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --context CexDbContext
```

After the migration is generated, add a backfill step **inside `Up()`** after `AddCheckConstraint`, before the closing brace:

```csharp
// Backfill: set MaxProfitCheckedAt only for already-entered signals.
migrationBuilder.Sql("UPDATE Signals SET MaxProfitCheckedAt = EntryHitAt WHERE EntryHitAt IS NOT NULL");
```

This ensures existing entered signals get their scan pointer anchored to `EntryHitAt`. Unentered signals remain `null`, consistent with the new lifecycle rule.

---

## Related Features

- **FindSignalCommand** — seeds `EntryPrice`, `SignalType`, `DetectedAt`; `MaxProfitCheckedAt` left `null` at record creation; `Leverage` defaults to 10
- **CheckSignalEntry** — sets `EntryHitAt` and overwrites `MaxProfitCheckedAt = hit.OpenTime`, anchoring the scan start
- **CheckSignalStopLoss** — sets `StopLossHitAt`, which is the upper scan bound for closed positions; **must run before `CheckSignalMaxProfit`** each cycle (see Technical Notes)

---

## Future

- Expose `MaxProfit` and `MaxProfitHitAt` via a query/API endpoint for front-end display
- Configurable leverage per signal at creation time (currently all signals default to 10)
- Early-exit optimisation: break the candle loop when all stopped-out candidates are fully scanned and no open candidates remain
- Notification on new all-time high profit via `INotifier`
