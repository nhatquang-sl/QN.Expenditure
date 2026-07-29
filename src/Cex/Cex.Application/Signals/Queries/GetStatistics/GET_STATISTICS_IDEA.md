# GetSignalsStatistics

> See [SIGNALS.md](../../SIGNALS.md) for the full entity schema and column reference.

## Overview

`GetSignalsStatisticsQuery` returns aggregated signal counts and averages grouped by time period (today, yesterday, this week, last week, this month, last month). Supports optional `Interval` and `SignalType` filters. Fetches all relevant signals in a single DB round-trip, then partitions and aggregates in memory.

**Module Location**: `src/Cex/Cex.Application/Signals/Queries/GetStatistics/`

---

## Response Shape

```csharp
public record SignalStatisticInfo
{
    public int TotalSignals { get; init; }       // Count of signals detected in period
    public int TotalEntries { get; init; }        // Count where EntryHitAt IS NOT NULL
    public int TotalMaxProfitHits { get; init; }  // Count where MaxProfitHitAt IS NOT NULL
    public int TotalStopLossHits { get; init; }   // Count where StopLossHitAt IS NOT NULL
    public decimal AvgEntryPrice { get; init; }   // Avg EntryPrice for entered signals; 0 if none
    public decimal AvgMaxProfit { get; init; }    // Avg MaxProfit for signals with MaxProfitHitAt set; 0 if none
}

public record SignalStatistics
{
    public SignalStatisticInfo Today { get; init; }
    public SignalStatisticInfo Yesterday { get; init; }
    public SignalStatisticInfo ThisWeek { get; init; }
    public SignalStatisticInfo LastWeek { get; init; }
    public SignalStatisticInfo ThisMonth { get; init; }
    public SignalStatisticInfo LastMonth { get; init; }
}
```

---

## Query Parameters

```csharp
public record GetSignalsStatisticsQuery(
    string? Interval = null,
    SignalType? SignalType = null,
    DateTime? AsOf = null) : IRequest<SignalStatistics>;
```

| Parameter | Default | Description |
|---|---|---|
| `Interval` | `null` | Optional filter on `Signal.Interval` |
| `SignalType` | `null` | Optional filter on `Signal.SignalType` |
| `AsOf` | `DateTime.UtcNow` | Reference "now" for period boundary computation — useful for deterministic testing |

---

## Algorithm

```
1. Compute 6 period boundaries relative to AsOf (UTC, see Period Computation below)
2. Fetch from DB:
     WHERE DetectedAt >= lastMonthStart AND DetectedAt <= now
     Apply Interval and SignalType filters if set
     Project: DetectedAt, EntryHitAt, MaxProfitHitAt, StopLossHitAt, EntryPrice, MaxProfit
3. In-memory: partition the result by period boundary, aggregate each partition
4. Return SignalStatistics with one SignalStatisticInfo per period
```

Single DB round-trip for all 6 periods. The `lastMonthStart` lower bound captures the widest required window.

---

## Period Computation

- Week starts on **Monday** (ISO 8601).
- All boundaries are UTC midnight.
- Period ranges are **left-inclusive, right-exclusive** (`>= start AND < end`).
- "Open" periods (Today, ThisWeek, ThisMonth) use `now` as the right bound (inclusive).

| Period | Start | End |
|---|---|---|
| Today | `todayStart` | `now` |
| Yesterday | `todayStart - 1d` | `todayStart` |
| ThisWeek | `thisMonday` | `now` |
| LastWeek | `thisMonday - 7d` | `thisMonday` |
| ThisMonth | `1st of current month` | `now` |
| LastMonth | `1st of prior month` | `1st of current month` |

---

## Aggregation Logic

For each period:
- `TotalSignals` = count where `DetectedAt` is in period
- `TotalEntries` = count where `EntryHitAt IS NOT NULL`
- `TotalMaxProfitHits` = count where `MaxProfitHitAt IS NOT NULL`
- `TotalStopLossHits` = count where `StopLossHitAt IS NOT NULL`
- `AvgEntryPrice` = average `EntryPrice` for entered signals; `0` if none entered
- `AvgMaxProfit` = average `MaxProfit` for signals where `MaxProfitHitAt IS NOT NULL`; `0` if none

Returns a zero-value `SignalStatisticInfo` (all `0`) for periods with no signals.

---

## Implementation Checklist

- [x] `GetSignalsStatisticsQuery` record + `GetSignalsStatisticsQueryHandler` in `GetSignalsStatisticsQuery.cs`
- [ ] API endpoint in `SignalsController` (`GET /api/signals/statistics`)
- [ ] Frontend component on `/signals` page, positioned below the search bar

---

## Related Features

- **GetSignals** — sibling query for paginated signal data
- **FindSignalCommand** — creates the `Signal` rows this query aggregates
- **CheckSignalEntry / CheckSignalStopLoss / CheckSignalMaxProfit** — set the outcome fields that this query counts
