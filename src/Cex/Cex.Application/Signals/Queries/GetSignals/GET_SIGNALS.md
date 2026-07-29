# GetSignals

> See [SIGNALS.md](../../SIGNALS.md) for the full entity schema, column reference, and signal lifecycle.

## Overview

`GetSignalsQuery` returns a paginated list of `Signal` rows with required date-range filtering and optional `Interval`/`SignalType` filters. Default sort is `CreatedAt` descending.

**Module Location**: `src/Cex/Cex.Application/Signals/Queries/GetSignals/`
**Scope**: All signals (system-wide, not user-scoped). BTCUSDT only in v1.

---

## `SignalDto`

All `DateTime` fields are serialized as Unix timestamp milliseconds (`long`) via `ToUnixTimestampMilliseconds()`. Duration fields (`EntryHitAfterMinutes`, `MaxProfitHitAfterMinutes`, `StopLossHitAfterMinutes`) are read directly from the persisted columns — no in-memory computation. `-1` means the corresponding event has not yet occurred.

```csharp
public record SignalDto
{
    public int Id { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public string SignalType { get; init; } = string.Empty;  // "Long" or "Short"
    public long DetectedAt { get; init; }                    // Unix ms
    public decimal RsiValue { get; init; }
    public decimal PreviousRsiValue { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal TakeProfit { get; init; }
    public int Leverage { get; init; }
    public decimal MaxProfit { get; init; }
    public long? MaxProfitHitAt { get; init; }               // Unix ms; null if MaxProfit == 0
    public long? EntryHitAt { get; init; }                   // Unix ms; null if not entered
    public long? StopLossHitAt { get; init; }                // Unix ms; null if not stopped
    public long? TakeProfitHitAt { get; init; }              // Unix ms; null (future)
    public long CreatedAt { get; init; }                     // Unix ms
    public int EntryHitAfterMinutes { get; init; }           // -1 until entry hit
    public int MaxProfitHitAfterMinutes { get; init; }       // -1 until entry hit
    public int StopLossHitAfterMinutes { get; init; }        // -1 until stopped out
}
```

---

## Business Rules

1. `From` and `To` are **required**; validation rejects requests missing either. `From <= To`.
2. Filter is on `DetectedAt` — inclusive range: `From <= DetectedAt <= To`.
3. `Interval` is optional. When provided, must be one of: `1min`, `5min`, `15min`, `30min`, `1hour`, `4hour`, `1day`.
4. `SignalType` is optional. When provided, must be `Long` or `Short`.
5. Sorting:
   - `SortBy` accepted values: `createdAt` (default), `entryHitAfterMinutes`, `maxProfitHitAfterMinutes`, `stopLossHitAfterMinutes`
   - `SortOrder` accepted values: `asc`, `desc` (case-insensitive). Defaults to `desc`.
   - Sorting is on persisted columns — SQL-level ordering, pagination-correct.
   - `-1` (not-yet-hit sentinel) sorts first in ASC, last in DESC.
6. Pagination: `PageNumber` defaults to 1, `PageSize` defaults to 20 (max 100).

---

## Data Access

```csharp
var query = dbContext.Signals
    .AsNoTracking()
    .Where(s => s.DetectedAt >= request.From && s.DetectedAt <= request.To);

if (!string.IsNullOrEmpty(request.Interval))
    query = query.Where(s => s.Interval == request.Interval);

if (request.SignalType.HasValue)
    query = query.Where(s => s.SignalType == request.SignalType.Value);

var descending = !string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

var orderedQuery = request.SortBy switch
{
    "entryHitAfterMinutes"    => descending ? query.OrderByDescending(s => s.EntryHitAfterMinutes)    : query.OrderBy(s => s.EntryHitAfterMinutes),
    "maxProfitHitAfterMinutes" => descending ? query.OrderByDescending(s => s.MaxProfitHitAfterMinutes) : query.OrderBy(s => s.MaxProfitHitAfterMinutes),
    "stopLossHitAfterMinutes" => descending ? query.OrderByDescending(s => s.StopLossHitAfterMinutes)  : query.OrderBy(s => s.StopLossHitAfterMinutes),
    _                         => descending ? query.OrderByDescending(s => s.CreatedAt)                : query.OrderBy(s => s.CreatedAt),
};
```

DateTime-to-timestamp conversion is done in-memory after the DB fetch (EF Core cannot translate `ToUnixTimestampMilliseconds()` to SQL). Sorting and pagination happen at the SQL level.

---

## Backend Architecture

**Files in `src/Cex/Cex.Application/Signals/Queries/GetSignals/`**:
- `GetSignalsQueryHandler.cs` — `GetSignalsQuery` record + `GetSignalsQueryHandler` colocated
- `GetSignalsQueryValidator.cs` — FluentValidation
- `SignalDto.cs` — response DTO

**API endpoint** (`src/WebAPI/Controllers/SignalsController.cs`):

| Method | Route | Response |
|---|---|---|
| GET | `/api/signals?from=...&to=...` | `PaginatedList<SignalDto>` |

Query parameters: `from` (required), `to` (required), `interval` (optional), `signalType` (optional), `pageNumber` (default 1), `pageSize` (default 20), `sortBy` (optional), `sortOrder` (optional, default `desc`).

---

## Frontend Architecture

**Feature location**: `src/WebUI.React/src/features/signal/signals/`

### Sortable Columns

Column `id` is used directly as the `sortBy` value sent to the API — no separate mapping needed:

```typescript
const COLUMNS: Column[] = [
  { id: 'detectedAt',              label: 'Detected At' },
  { id: 'type',                    label: 'Type',          align: 'center' },
  { id: 'interval',                label: 'Interval',      align: 'center' },
  { id: 'entryPrice',              label: 'Entry Price',   align: 'right' },
  { id: 'stopLoss',                label: 'Stop Loss',     align: 'right' },
  { id: 'maxProfit',               label: 'Max Profit %',  align: 'right' },
  { id: 'maxProfitHitAfterMinutes', label: 'Max Profit Hit', align: 'right', sortable: true },
  { id: 'entryHitAfterMinutes',    label: 'Entry Hit',     align: 'center', sortable: true },
  { id: 'createdAt',               label: 'Created At',    sortable: true },
  { id: 'stopLossHitAfterMinutes', label: 'SL Hit',        align: 'center', sortable: true },
];
```

### Duration Display

Duration fields are converted from minutes to milliseconds for `formatDuration`. A guard of `>= 0` handles the `-1` sentinel (renders `—` for not-yet-hit):

```typescript
entryHit: item.entryHitAfterMinutes >= 0 && entryHitDate ? (
  <Tooltip title={...}>
    <Typography>{formatDuration(item.entryHitAfterMinutes * 60_000)}</Typography>
  </Tooltip>
) : '—',
```

### Sort Cycle

Inactive → ASC → DESC → reset (calls `onSortChange('', 'desc')`). The parent coerces the empty string back to `createdAt`:

```typescript
const handleSortChange = (sortBy: string, sortOrder: 'asc' | 'desc') => {
  setSignalQuery((prev) => ({
    ...prev,
    sortBy: sortBy || 'createdAt',
    sortOrder: sortBy ? sortOrder : 'desc',
    pageNumber: 1,
  }));
};
```

### Initial State

Uses the lazy initializer form of `useState` so date computations run only once (on mount):

```typescript
const [signalQuery, setSignalQuery] = useState(() => ({
  from: new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1).toISOString(),
  to: new Date().toISOString(),
  // ...
}));
```

---

## Performance Considerations

- **`IX_Signals_Symbol_Interval_DetectedAt`** (unique) partially covers the query when `Interval` is provided.
- **Sort indexes**: `IX_Signals_EntryHitAfterMinutes`, `IX_Signals_MaxProfitHitAfterMinutes`, `IX_Signals_StopLossHitAfterMinutes` support efficient sorting on the duration columns.
- **`.AsNoTracking()`** — avoids change-tracker overhead on read-only queries.
- **Two-step fetch** — DB projection to anonymous type, then in-memory mapping to `SignalDto` (required because `ToUnixTimestampMilliseconds()` cannot be translated by EF Core).

---

## Error Handling

| Scenario | Handling |
|---|---|
| Missing `from` or `to` | FluentValidation 400 with field-level error |
| `from > to` | FluentValidation 400: "To date must be >= From date" |
| Invalid `interval` value | FluentValidation 400 with allowed values list |
| Invalid `signalType` | FluentValidation 400: "SignalType must be Long or Short" |
| Invalid `sortBy` or `sortOrder` | FluentValidation 400 with allowed values |
| No matching records | 200 with empty `Items` and `TotalCount = 0` |

---

## Implementation Checklist

### Application Layer
- [x] `SignalDto.cs`
- [x] `GetSignalsQueryHandler.cs` (query record + handler colocated)
- [x] `GetSignalsQueryValidator.cs`

### Domain & Infrastructure
- [x] `EntryHitAfterMinutes`, `MaxProfitHitAfterMinutes`, `StopLossHitAfterMinutes` added to `Signal`
- [x] Indexes on all three columns in `SignalConfiguration`
- [x] Migration `AddSignalDurationColumns` with backfill via `ISNULL(DATEDIFF(...), -1)`
- [x] `CheckSignalEntry` sets `EntryHitAfterMinutes` on hit
- [x] `CheckSignalMaxProfit` sets `MaxProfitHitAfterMinutes` on new high
- [x] `CheckSignalStopLoss` sets `StopLossHitAfterMinutes` on hit

### API Layer
- [x] `SignalsController.cs` — `GET /api/signals`

### Frontend
- [x] React feature: `src/WebUI.React/src/features/signal/signals/`
- [x] Column-click sorting with asc/desc toggle
- [x] Default sort state (`createdAt DESC`) reflected in column header on page load

---

## Technical Notes

- **`SignalType` is stored as a string** in the DB via `HasConversion<string>()`. EF Core handles the enum-to-string comparison transparently when filtering.
- **`Interval` is a plain string** — the validator enforces known values; the handler compares strings directly.
- **No user-scoping** — unlike `TradeHistory`, signals are system-wide. `[Authorize]` ensures authentication but results are not filtered per user.
- **All timestamps in the DTO are Unix milliseconds (`long`)** to avoid timezone serialization issues.

---

## Related Features

- **FindSignal** — creates `Signal` entries that this query reads
- **CheckSignalEntry** — sets `EntryHitAt` and `EntryHitAfterMinutes`
- **CheckSignalStopLoss** — sets `StopLossHitAt` and `StopLossHitAfterMinutes`
- **CheckSignalMaxProfit** — sets `MaxProfit`, `MaxProfitHitAt`, `MaxProfitHitAfterMinutes`
- **GetSignalsStatistics** — sibling query returning period-aggregated stats

---

## Future

- Add export to CSV
- Add Symbol filter when multi-symbol support is added
- Consider non-clustered index on `DetectedAt DESC` for date-range-only queries without interval filter
