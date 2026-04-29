# RSI Divergence Signal Record Feature

## Overview

When the `RunIndicatorCommandHandler` detects an RSI divergence and sends a Telegram notification, it should also persist the signal details to the database. This allows historical analysis, back-testing, and tracking whether a signal's stop-loss or take-profit level was eventually hit.

---

## Property Name Review

The user's proposed properties alongside suggested alternatives:

| Proposed        | Suggested              | Reason                                                                                            |
| --------------- | ---------------------- | ------------------------------------------------------------------------------------------------- |
| `Type`          | **`SignalType`**       | Avoids collision with the C# `Type` keyword; clearer intent                                       |
| `Time`          | **`DetectedAt`**       | Conventional `*At` suffix for timestamps; "detected" reflects when the divergence candle appeared |
| `PreviousTime`  | **`PreviousCandleAt`** | Explicit that this is the timestamp of the prior peak/trough candle                               |
| `Rsi`           | **`RsiValue`**         | Distinguishes the numeric value from a generic property named after the indicator                 |
| `PreviousRsi`   | **`PreviousRsiValue`** | Consistent with `RsiValue`                                                                        |
| `EntryPrice`    | `EntryPrice` ✓         | Clear and standard trading terminology                                                            |
| `Stoploss`      | **`StopLoss`**         | Fix casing — `StopLoss` is the conventional compound word in trading                              |
| `Target`        | **`TakeProfit`**       | More precise trading term; `Target` is ambiguous                                                  |
| `StopLossHitAt` | `StopLossHitAt` ✓      | Clear and consistent with `*At` convention                                                        |
| `TargetHitAt`   | **`TakeProfitHitAt`**  | Rename to match `TakeProfit`                                                                      |

**Additional columns recommended** (not in original proposal):

| Column      | Type                           | Reason                                                                                                |
| ----------- | ------------------------------ | ----------------------------------------------------------------------------------------------------- |
| `Id`        | `int` (identity)               | Simple surrogate primary key — no natural composite key fits                                          |
| `Symbol`    | `string`                       | Currently hardcoded as `"BTCUSDT"` in the handler, but must be stored for future multi-symbol support |
| `Interval`  | `IntervalType` (enum → string) | The timeframe that produced the signal (e.g. `FourHours`, `OneHour`); critical for filtering          |
| `CreatedAt` | `DateTime`                     | Audit timestamp set automatically by the DB via `GETUTCDATE()` — not set in application code          |

---

## Data Model

### Entity: `SignalRecord` (New)

```
Id               int          PK, identity
Symbol           string       e.g. "BTCUSDT"
Interval         IntervalType e.g. FourHours, OneHour (stored as string)
SignalType       SignalType   Long | Short (stored as string)
DetectedAt       DateTime     OpenTime of the divergence candle (div.Time)
PreviousCandleAt DateTime     OpenTime of the prior peak/trough (div.PreviousTime)
RsiValue         decimal      RSI of the divergence candle (div.Rsi)
PreviousRsiValue decimal      RSI of the previous peak/trough (rsiValues[div.PreviousTime])
EntryPrice       decimal      Close price (short) or Open price (long) of the last candle
StopLoss         decimal      Computed at detection time (see Business Rules)
TakeProfit       decimal      Computed at detection time (see Business Rules)
EntryHitAt           DateTime?    Nullable — set when price reaches EntryPrice; set by a future monitoring job
StopLossHitAt        DateTime?    Nullable — set by a future monitoring job
TakeProfitHitAt      DateTime?    Nullable — set by a future monitoring job
CreatedAt            DateTime     UTC timestamp set by DB default (GETUTCDATE()) on insert
LastCheckedCandleAt  DateTime     Set to DateTime.UtcNow on insert (same as CreatedAt); updated by CheckSignalEntry job after each check
```

### Enum: `SignalType` (New, in `Cex.Domain`)

```csharp
public enum SignalType { Long, Short }
```

Maps to divergence detection:

- `DivergenceType.Peak` → `SignalType.Short` (bearish divergence)
- `DivergenceType.Trough` → `SignalType.Long` (bullish divergence)

### Business Rules

**StopLoss calculation** (matches the existing "Liquidation 8x10" logic in the Telegram message):

- Short signal: `StopLoss = EntryPrice × 1.08`
- Long signal: `StopLoss = EntryPrice × 0.92`

**TakeProfit calculation** — ⚠️ Not currently defined in the codebase. A symmetric 8% move is a natural default:

- Short signal: `TakeProfit = EntryPrice × 0.92`
- Long signal: `TakeProfit = EntryPrice × 1.08`

> Confirm the TakeProfit formula before implementation.

**Duplicate prevention**: If a signal for the same `Symbol + Interval + DetectedAt` already exists, skip saving (idempotency guard for retried scheduled jobs).

**`StopLossHitAt` / `TakeProfitHitAt`**: These are **not** set by `RunIndicatorCommandHandler`. They are reserved for a future price-monitoring background job.

---

## Backend Architecture

### Domain Layer

**New file**: `src/Cex/Cex.Domain/Entities/SignalRecord.cs`

```csharp
public class SignalRecord
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public IntervalType Interval { get; set; }
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

**New file**: `src/Cex/Cex.Domain/Enums/SignalType.cs`

```csharp
public enum SignalType { Long, Short }
```

### Infrastructure Layer

**New file**: `src/Cex/Cex.Infrastructure/Data/Configurations/SignalRecordConfiguration.cs`

```csharp
public class SignalRecordConfiguration : IEntityTypeConfiguration<SignalRecord>
{
    public void Configure(EntityTypeBuilder<SignalRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Interval).HasConversion<string>();
        builder.Property(x => x.SignalType).HasConversion<string>();
        builder.Property(x => x.Symbol).HasMaxLength(20);
        builder.Property(x => x.RsiValue).HasPrecision(10, 4);
        builder.Property(x => x.PreviousRsiValue).HasPrecision(10, 4);
        builder.Property(x => x.EntryPrice).HasPrecision(18, 8);
        builder.Property(x => x.StopLoss).HasPrecision(18, 8);
        builder.Property(x => x.TakeProfit).HasPrecision(18, 8);
        builder.Property(x => x.DetectedAt).HasPrecision(0);
        builder.Property(x => x.PreviousCandleAt).HasPrecision(0);
        builder.Property(x => x.EntryHitAt).HasPrecision(0);
        builder.Property(x => x.StopLossHitAt).HasPrecision(0);
        builder.Property(x => x.TakeProfitHitAt).HasPrecision(0);
        builder.Property(x => x.CreatedAt).HasPrecision(0).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.LastCheckedCandleAt).HasPrecision(0).HasDefaultValueSql("GETUTCDATE()");

        // Uniqueness guard: one signal record per symbol + interval + candle time
        builder.HasIndex(x => new { x.Symbol, x.Interval, x.DetectedAt }).IsUnique();
        builder.HasIndex(x => x.LastCheckedCandleAt);
    }
}
```

**Migration name**: `AddSignalRecord`

### Application Layer

No new Command/Query/Handler needed for the initial save — the insert is done directly inside `FindSignalCommandHandler.Handle()` via `ICexDbContext`, following the same pattern used in other handlers.

**`ICexDbContext`** — add one line:

```csharp
DbSet<SignalRecord> SignalRecords { get; }
```

**`FindSignalCommandHandler`** — inject `ICexDbContext dbContext`, then after `notifier.Notify(...)` in each case block, build and add a `SignalRecord` and call `SaveChangesAsync`.

### Algorithm (updated handler flow)

```
1. Fetch candles from KuCoin
2. Compute RSI values
3. Run divergence detection
4. If DivergenceType.None → return early (no save, no notify)
5. Compute entryPrice, stopLoss, takeProfit per signal type
6. Check DB for existing record with same Symbol + Interval + DetectedAt → skip if exists
7. Send Telegram notification (existing behaviour)
8. Insert SignalRecord to DB
9. SaveChangesAsync
```

### API Layer

No new endpoint in this iteration. `SignalRecord` data is for internal use and future analytics endpoints.

---

## Performance Considerations

- The unique index on `(Symbol, Interval, DetectedAt)` handles both fast duplicate-check queries and deduplication.
- The handler runs on a scheduled interval (e.g. every 4 hours) — a single insert per execution is negligible.

---

## Error Handling

| Scenario                            | Handling                                                                                                                   |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Duplicate signal (idempotent retry) | Caught by unique-index check before insert; silently skipped                                                               |
| DB save failure                     | Exception propagates to the hosted service scheduler; Telegram notification is still sent (sent before `SaveChangesAsync`) |
| `DivergenceType.None`               | Early return — no notification, no record saved                                                                            |

---

## Implementation Checklist

### Backend

- [ ] Create `SignalType` enum in `Cex.Domain`
- [ ] Create `SignalRecord` entity in `Cex.Domain`
- [ ] Create `SignalRecordConfiguration` EF Core config in `Cex.Infrastructure`
- [ ] Add `DbSet<SignalRecord> SignalRecords` to `ICexDbContext` and `CexDbContext`
- [ ] Add `ICexDbContext dbContext` to `FindSignalCommandHandler` constructor
- [ ] Update `FindSignalCommandHandler` to insert `SignalRecord` after notifying (set `LastCheckedCandleAt = DateTime.UtcNow`)
- [ ] Add migration: `AddSignalRecord`
- [ ] Confirm TakeProfit formula

### Testing

- [ ] Unit test: handler saves a `SignalRecord` with correct field values for `Peak` (Short)
- [ ] Unit test: handler saves a `SignalRecord` with correct field values for `Trough` (Long)
- [ ] Unit test: handler skips saving when `DivergenceType.None`
- [ ] Integration test: duplicate signal (same Symbol + Interval + DetectedAt) is not double-inserted

---

## Technical Notes

- `IntervalType` is defined in `Lib.ExternalServices` — it will be referenced as a dependency from `Cex.Domain`. If that creates a cross-layer concern, consider mirroring the enum in `Cex.Domain` and mapping at the handler level.
- `EntryHitAt`, `StopLossHitAt` and `TakeProfitHitAt` are future fields. They will be `NULL` for all records created by this feature; a separate monitoring command will update them.
- The Telegram message is sent **before** `SaveChangesAsync` to ensure notification delivery even if the DB insert fails. If atomicity between notification and persistence is required, reverse the order.

---

## Database Migration

```bash
dotnet ef migrations add AddSignalRecord \
  --project src/Cex/Cex.Infrastructure/Cex.Infrastructure.csproj \
  --startup-project src/WebAPI/WebAPI.csproj \
  --context CexDbContext
```

---

## Related Features

- `FindSignalCommand` — the triggering command
- `DivergenceCommand` / `DivergenceRsiPeakCommand` / `DivergenceRsiTroughCommand` — signal detection logic
- `StatisticTrade` entity — a similar but separate historical record concept; evaluate whether to unify in the future

---

## Future Enhancements

- `GET /api/signal-records` — paginated endpoint to view signal history
- Background job to monitor live price and set `StopLossHitAt` / `TakeProfitHitAt`
- Dashboard UI to display signal history with P&L outcome
- Multi-symbol support (remove hardcoded `"BTCUSDT"` from the handler)
