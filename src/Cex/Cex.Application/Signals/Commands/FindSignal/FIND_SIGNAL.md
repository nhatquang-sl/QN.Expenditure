# FindSignal

> See [SIGNALS.md](../../SIGNALS.md) for the full entity schema, enum definitions, column reference, and signal lifecycle.

## Overview

`FindSignalCommandHandler` detects RSI divergence on BTCUSDT candles for a given `IntervalType`, sends a Telegram notification, and persists a new `Signal` row to the database.

**Module Location**: `src/Cex/Cex.Application/Signals/Commands/FindSignal/`

---

## Business Rules

### Signal Classification

| Divergence | `SignalType` | Entry candle |
|---|---|---|
| `DivergenceType.Peak` (bearish) | `Short` | Close price of last candle (`candles[^1].ClosePrice`) |
| `DivergenceType.Trough` (bullish) | `Long` | Open price of last candle (`candles[^1].OpenPrice`) |

### Trade Level Formulas

| Field | Short | Long |
|---|---|---|
| `EntryPrice` | `candles[^1].ClosePrice` | `candles[^1].OpenPrice` |
| `StopLoss` | `entryPrice × 1.08` | `entryPrice × 0.92` |
| `TakeProfit` | `entryPrice × 0.92` | `entryPrice × 1.08` |

Both stop-loss and take-profit represent an 8% move against/in-favour of entry at 10× leverage ("Liquidation 8x10").

### Duplicate Prevention

If a row with the same `(Symbol, Interval, DetectedAt)` already exists, the insert is silently skipped. A `DbUpdateException` with "duplicate key" is caught and logged; execution continues normally. The unique index `IX_Signals_Symbol_Interval_DetectedAt` enforces the constraint.

---

## Algorithm

```
1. Fetch candles: GetKlines("BTCUSDT", command.Type, startDate, now)
2. Compute RSI values: RsiCommand(candles)
3. Detect divergence: DivergenceCommand(candles, rsiValues)
4. If DivergenceType.None -> return early (no notify, no save)
5. Compute entryPrice, stopLoss, takeProfit per SignalType
6. Send Telegram notification via INotifier
7. Insert Signal row (LastCheckedCandleAt = DateTime.UtcNow)
8. SaveChangesAsync; catch and log duplicate key silently
```

Telegram notification is sent **before** `SaveChangesAsync`. If the DB insert fails for a non-duplicate reason, the notification has already been sent.

---

## Backend Architecture

**File**: `src/Cex/Cex.Application/Signals/Commands/FindSignal/FindSignalCommand.cs`

```csharp
public record FindSignalCommand(IntervalType Type) : IRequest;

public class FindSignalCommandHandler(
    IKuCoinService kuCoinService,
    IOptions<KuCoinConfig> kuCoinConfig,
    ISender sender,
    INotifier notifier,
    ILogTrace logTrace,
    ICexDbContext dbContext)
    : IRequestHandler<FindSignalCommand>
```

`LastCheckedCandleAt` is set to `DateTime.UtcNow` on insert so `CheckSignalEntry` starts scanning from the creation time. `MaxProfitCheckedAt` is **not** set at creation — it is set by `CheckSignalEntry` when entry is confirmed.

---

## Error Handling

| Scenario | Handling |
|---|---|
| `DivergenceType.None` | Early return — no notification, no DB write |
| Duplicate `(Symbol, Interval, DetectedAt)` | `DbUpdateException` caught and logged; silently skipped |
| Non-duplicate DB failure | Exception propagates to `FindSignalService` |
| Telegram failure | Exception propagates — signal is not saved if notify throws |

---

## Implementation Checklist

- [x] `SignalType` enum in `Cex.Domain`
- [x] `Signal` entity in `Cex.Domain`
- [x] `SignalConfiguration` EF Core config in `Cex.Infrastructure`
- [x] `DbSet<Signal> Signals` in `ICexDbContext` and `CexDbContext`
- [x] `FindSignalCommandHandler` inserts `Signal` after notifying
- [x] Migration: `AddSignal`
- [x] Duplicate-key handling via unique index

---

## Technical Notes

- `IntervalType` is from `Lib.ExternalServices`; its `GetDescription()` extension produces the string stored in `Signal.Interval`.
- `Leverage` defaults to `10` on insert; no override at detection time.
- `EntryHitAt`, `StopLossHitAt`, `TakeProfitHitAt`, `MaxProfitHitAt` are all `NULL` on insert — set by monitoring commands.

---

## Related Features

- `CheckSignalEntry` — detects price reaching `EntryPrice`
- `CheckSignalStopLoss` — detects price crossing `StopLoss`
- `CheckSignalMaxProfit` — tracks best leveraged profit % since entry
- `DivergenceCommand` / `DivergenceRsiPeakCommand` / `DivergenceRsiTroughCommand` — divergence detection logic

---

## Future

- Multi-symbol support (remove hardcoded `"BTCUSDT"`)
- Configurable leverage per signal at detection time
- `CheckSignalTakeProfit` command for `TakeProfitHitAt`
