# Signals

> **Shared reference for the Signals module.** All feature docs in this folder reference this file for entity schema, enum definitions, column meanings, lifecycle, and DB configuration.

## Overview

The Signals module detects RSI divergence patterns on BTCUSDT candles and persists each detected signal for lifecycle tracking. A signal progresses from detection through entry confirmation, stop-loss hit, and continuous max-profit tracking.

Four commands run on a schedule inside `FindSignalService` (`src/WebAPI/HostedServices/FindSignalService.cs`):

| Command | Frequency | Purpose |
|---|---|---|
| `FindSignalCommand` | Every N hours (per `IntervalType`) | Detect divergence; create new `Signal` rows |
| `CheckSignalEntryCommand` | Every 1 min | Detect when price reaches `EntryPrice` |
| `CheckSignalStopLossCommand` | Every 1 min | Detect when price crosses `StopLoss` |
| `CheckSignalMaxProfitCommand` | Every 1 min | Track best leverage-adjusted profit % per entered position |

**Execution order per cycle (correctness invariant):**

```
CheckSignalEntry → CheckSignalStopLoss → CheckSignalMaxProfit
```

`CheckSignalStopLoss` must run before `CheckSignalMaxProfit` so `StopLossHitAt` is current when `CheckSignalMaxProfit` caps its scan window.

---

## Enum: `SignalType`

**File**: `src/Cex/Cex.Domain/Enums/SignalType.cs`

```csharp
public enum SignalType { Long, Short }
```

| Value | Meaning | Divergence source |
|---|---|---|
| `Long` | Bullish signal — expect price to rise | `DivergenceType.Trough` |
| `Short` | Bearish signal — expect price to fall | `DivergenceType.Peak` |

---

## Entity: `Signal`

**File**: `src/Cex/Cex.Domain/Entities/Signal.cs`

```csharp
public class Signal
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
    public decimal MaxProfit { get; set; } = 0;
    public DateTime? MaxProfitHitAt { get; set; }
    public DateTime? MaxProfitCheckedAt { get; set; }
    public DateTime? EntryHitAt { get; set; }
    public DateTime? StopLossHitAt { get; set; }
    public DateTime? TakeProfitHitAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastCheckedCandleAt { get; set; }
    public int EntryHitAfterMinutes { get; set; } = -1;
    public int MaxProfitHitAfterMinutes { get; set; } = -1;
    public int StopLossHitAfterMinutes { get; set; } = -1;
}
```

---

## Column Reference

### Identity & Classification

| Column | C# Type | SQL Type | Nullable | Default | Written by | Description |
|---|---|---|---|---|---|---|
| `Id` | `int` | `int IDENTITY` | No | — | DB | Surrogate primary key |
| `Symbol` | `string` | `nvarchar(20)` | No | — | `FindSignalCommand` | Trading pair. Currently hardcoded `"BTCUSDT"` |
| `Interval` | `string` | `nvarchar(20)` | No | — | `FindSignalCommand` | Candle timeframe used for divergence detection. String from `IntervalType.GetDescription()`, e.g. `"4hour"`, `"1hour"`, `"1day"` |
| `SignalType` | `SignalType` | `nvarchar(max)` | No | — | `FindSignalCommand` | `"Long"` or `"Short"`. Stored as string via `HasConversion<string>()` |

### Divergence Detection

| Column | C# Type | SQL Type | Nullable | Default | Written by | Description |
|---|---|---|---|---|---|---|
| `DetectedAt` | `DateTime` | `datetime2(0)` | No | — | `FindSignalCommand` | `OpenTime` of the divergence candle — the most recent peak/trough that completes the pattern |
| `PreviousCandleAt` | `DateTime` | `datetime2(0)` | No | — | `FindSignalCommand` | `OpenTime` of the prior peak/trough candle against which divergence is measured |
| `RsiValue` | `decimal` | `decimal(10,4)` | No | — | `FindSignalCommand` | RSI of the `DetectedAt` candle |
| `PreviousRsiValue` | `decimal` | `decimal(10,4)` | No | — | `FindSignalCommand` | RSI of the `PreviousCandleAt` candle |

### Trade Levels (set at detection, immutable thereafter)

| Column | C# Type | SQL Type | Nullable | Default | Written by | Description |
|---|---|---|---|---|---|---|
| `EntryPrice` | `decimal` | `decimal(18,8)` | No | — | `FindSignalCommand` | Planned entry price. **Short**: close price of last candle; **Long**: open price of last candle |
| `StopLoss` | `decimal` | `decimal(18,8)` | No | — | `FindSignalCommand` | Stop-loss threshold. **Short**: `EntryPrice × 1.08`; **Long**: `EntryPrice × 0.92` (8% unfavourable move) |
| `TakeProfit` | `decimal` | `decimal(18,8)` | No | — | `FindSignalCommand` | Take-profit threshold. **Short**: `EntryPrice × 0.92`; **Long**: `EntryPrice × 1.08` (8% favourable move) |
| `Leverage` | `int` | `int` | No | `10` | `FindSignalCommand` | Leverage multiplier applied to raw price movement for profit % calculation. Range: 1–125. Enforced by `CK_Signals_Leverage` |

### Outcome Tracking

| Column | C# Type | SQL Type | Nullable | Default | Written by | Description |
|---|---|---|---|---|---|---|
| `EntryHitAt` | `DateTime?` | `datetime2(0)` | Yes | `NULL` | `CheckSignalEntry` | `OpenTime` of the first 1-min candle where price reached `EntryPrice`. `NULL` = not yet entered |
| `StopLossHitAt` | `DateTime?` | `datetime2(0)` | Yes | `NULL` | `CheckSignalStopLoss` | `OpenTime` of the first 1-min candle where price crossed `StopLoss`. `NULL` = not stopped out |
| `TakeProfitHitAt` | `DateTime?` | `datetime2(0)` | Yes | `NULL` | *(future)* | Reserved for a future `CheckSignalTakeProfit` command |
| `MaxProfit` | `decimal` | `decimal(10,4)` | No | `0` | `CheckSignalMaxProfit` | Best leverage-adjusted profit % achieved since entry. `0` = price never moved favourably |
| `MaxProfitHitAt` | `DateTime?` | `datetime2(0)` | Yes | `NULL` | `CheckSignalMaxProfit` | `OpenTime` of the candle where `MaxProfit` was last updated. `NULL` if `MaxProfit` is still `0` |

### Duration Columns (persisted, not computed at query time)

| Column | C# Type | SQL Type | Nullable | Default | Written by | Description |
|---|---|---|---|---|---|---|
| `EntryHitAfterMinutes` | `int` | `int` | No | `-1` | `CheckSignalEntry` | `(int)(EntryHitAt − DetectedAt).TotalMinutes`. **`-1` = not yet entered** |
| `MaxProfitHitAfterMinutes` | `int` | `int` | No | `-1` | `CheckSignalMaxProfit` | `(int)(MaxProfitHitAt − DetectedAt).TotalMinutes`. Updated on every new `MaxProfit` high. **`-1` = no entry yet** |
| `StopLossHitAfterMinutes` | `int` | ``int` | No | `-1` | `CheckSignalStopLoss` | `(int)(StopLossHitAt − DetectedAt).TotalMinutes`. **`-1` = not stopped out** |

Stored as integers so they can be sorted at the SQL level without EF Core date arithmetic functions.

### Scan Pointers (internal — not exposed in API responses)

| Column | C# Type | SQL Type | Nullable | Default | Written by | Description |
|---|---|---|---|---|---|---|
| `LastCheckedCandleAt` | `DateTime` | `datetime2(0)` | No | `GETUTCDATE()` | `CheckSignalEntry` / `CheckSignalStopLoss` | Shared scan pointer. Pre-entry: advanced by `CheckSignalEntry` after each batch. Post-entry: advanced by `CheckSignalStopLoss`. Never moves backward |
| `MaxProfitCheckedAt` | `DateTime?` | `datetime2(0)` | Yes | `NULL` | `CheckSignalEntry` / `CheckSignalMaxProfit` | Scan pointer for `CheckSignalMaxProfit`. Set to `EntryHitAt` by `CheckSignalEntry` on entry; advanced by `CheckSignalMaxProfit` each run. `NULL` until entry is hit |
| `CreatedAt` | `DateTime` | `datetime2(0)` | No | `GETUTCDATE()` | DB default | UTC insert timestamp. Never assigned in application code |

---

## Database Table: `Signals`

### Constraints

| Type | Name | Definition |
|---|---|---|
| Primary Key | `PK_Signals` | `Id` |
| Unique Index | `IX_Signals_Symbol_Interval_DetectedAt` | `(Symbol, Interval, DetectedAt)` — one signal per symbol + interval + detection candle; idempotency guard |
| Check Constraint | `CK_Signals_Leverage` | `[Leverage] >= 1 AND [Leverage] <= 125` |

### Indexes

| Index | Column | Purpose |
|---|---|---|
| `IX_Signals_LastCheckedCandleAt` | `LastCheckedCandleAt` | `ORDER BY LastCheckedCandleAt ASC TAKE 100` in `CheckSignalEntry` and `CheckSignalStopLoss` |
| `IX_Signals_MaxProfitCheckedAt` | `MaxProfitCheckedAt` | `ORDER BY MaxProfitCheckedAt ASC TAKE 100` in `CheckSignalMaxProfit` |
| `IX_Signals_EntryHitAfterMinutes` | `EntryHitAfterMinutes` | Sort column in `GetSignals` |
| `IX_Signals_MaxProfitHitAfterMinutes` | `MaxProfitHitAfterMinutes` | Sort column in `GetSignals` |
| `IX_Signals_StopLossHitAfterMinutes` | `StopLossHitAfterMinutes` | Sort column in `GetSignals` |

### EF Core Configuration

**File**: `src/Cex/Cex.Infrastructure/Data/Configurations/SignalConfiguration.cs`

All `DateTime`/`DateTime?` properties use `HasPrecision(0)` → `datetime2(0)` (second-level precision matching candle `OpenTime` granularity). Sub-second precision is never needed.

---

## Signal Lifecycle

```
[Created by FindSignalCommand]
        |
        v
  EntryHitAt = NULL               <-- CheckSignalEntry scans every minute
  LastCheckedCandleAt = CreatedAt      (WHERE EntryHitAt IS NULL)
        |
        | Price reaches EntryPrice
        v
  EntryHitAt            = hit.OpenTime
  EntryHitAfterMinutes  computed
  LastCheckedCandleAt   = hit.OpenTime  (anchor for stop-loss scan)
  MaxProfitCheckedAt    = hit.OpenTime  (anchor for max-profit scan)
        |
        +------------------------------------------+
        |                                          |
        v                                          v
  CheckSignalStopLoss                  CheckSignalMaxProfit
  (EntryHitAt NOT NULL                 (EntryHitAt NOT NULL AND
   AND StopLossHitAt IS NULL)           scan not complete)
        |                                          |
        | Price crosses StopLoss                   | New profit high found
        v                                          v
  StopLossHitAt           = hit.OpenTime  MaxProfit updated
  StopLossHitAfterMinutes set             MaxProfitHitAt updated
  LastCheckedCandleAt     advanced        MaxProfitHitAfterMinutes updated
                                          MaxProfitCheckedAt advanced
                                          (capped at StopLossHitAt for closed positions)
```

**Terminal states:**

- `StopLossHitAt IS NOT NULL` — position stopped out. `CheckSignalStopLoss` no longer processes it.
- `MaxProfitCheckedAt >= StopLossHitAt` — max-profit scan complete for a closed position. `CheckSignalMaxProfit` no longer processes it.
- Open positions (`StopLossHitAt IS NULL`) continue to be scanned by `CheckSignalMaxProfit` indefinitely.
- `TakeProfitHitAt` is unused — reserved for a future `CheckSignalTakeProfit` command.

---

## Module Structure

```
src/Cex/Cex.Application/Signals/
├── SIGNALS.md                              <- shared reference (this file)
├── Commands/
│   ├── FindSignal/
│   │   ├── FindSignalCommand.cs
│   │   └── FIND_SIGNAL.md
│   ├── CheckSignalEntry/
│   │   ├── CheckSignalEntryCommand.cs
│   │   └── CHECK_SIGNAL_ENTRY.md
│   ├── CheckSignalStopLoss/
│   │   ├── CheckSignalStopLossCommand.cs
│   │   └── CHECK_SIGNAL_STOP_LOSS.md
│   ├── CheckSignalMaxProfit/
│   │   ├── CheckSignalMaxProfitCommand.cs
│   │   └── CHECK_SIGNAL_MAX_PROFIT.md
│   └── Rsi/
│       └── (RSI/divergence calculation commands)
└── Queries/
    ├── GetSignals/
    │   ├── GetSignalsQueryHandler.cs
    │   ├── GetSignalsQueryValidator.cs
    │   ├── SignalDto.cs
    │   └── GET_SIGNALS.md
    └── GetStatistics/
        ├── GetSignalsStatisticsQuery.cs
        └── GET_STATISTICS_IDEA.md
```

## Related Domain Files

| File | Purpose |
|---|---|
| `src/Cex/Cex.Domain/Entities/Signal.cs` | Entity definition |
| `src/Cex/Cex.Domain/Enums/SignalType.cs` | `SignalType` enum |
| `src/Cex/Cex.Infrastructure/Data/Configurations/SignalConfiguration.cs` | EF Core table mapping and constraints |
| `src/WebAPI/HostedServices/FindSignalService.cs` | Hosted service: schedules all four commands in order |
| `src/WebAPI/Controllers/SignalsController.cs` | REST endpoints for signal data |
