# GetSignals

## Overview

Query all `Signal` entries with required date-range filtering and optional `Interval` / `SignalType` filters. This is the first read endpoint for the Signal module, enabling users to review detected signals over a given time period. Results are paginated with configurable sorting. Default sort is `CreatedAt` descending.

**Module Location**: `src/Cex/Cex.Application/Signal/Queries/GetSignals/`
**Scope**: All signals (system-wide, not user-scoped). BTCUSDT only in v1.

---

## Data Model

### Entity: Signal

Two persisted columns added: `EntryHitAfterMinutes` and `MaxProfitHitAfterMinutes`.

Relevant columns for filtering and sorting:

| Column | Type | Nullable | Notes |
|---|---|---|---|
| DetectedAt | datetime2(0) | No | Primary filter: `FROM <= DetectedAt <= TO` |
| Interval | nvarchar(20) | No | Optional filter. Stored values: `1min`, `5min`, `15min`, `30min`, `1hour`, `4hour`, `1day` |
| SignalType | nvarchar(max) | No | Optional filter. Stored values: `Long`, `Short` (string conversion of enum) |
| EntryHitAfterMinutes | int | No | Persisted: `(int)(EntryHitAt - DetectedAt).TotalMinutes`. Set by `CheckSignalEntry` when entry hits. Default `-1` until hit. |
| MaxProfitHitAfterMinutes | int | No | Persisted: `(int)(MaxProfitHitAt - DetectedAt).TotalMinutes`. Updated by `CheckSignalMaxProfit` whenever a new max profit is recorded. Default `-1` until entry hits. |
| StopLossHitAfterMinutes | int | No | Persisted: `(int)(StopLossHitAt - DetectedAt).TotalMinutes`. Set by `CheckSignalStopLoss` when stop loss hits. Default `-1` until hit. |

### DTOs

```csharp
namespace Cex.Application.Signals.Queries.GetSignals;

public record SignalDto
{
    public int Id { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public string SignalType { get; init; } = string.Empty;
    public long DetectedAt { get; init; }
    public decimal RsiValue { get; init; }
    public decimal PreviousRsiValue { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal TakeProfit { get; init; }
    public int Leverage { get; init; }
    public decimal MaxProfit { get; init; }
    public long? MaxProfitHitAt { get; init; }
    public long? EntryHitAt { get; init; }
    public long? StopLossHitAt { get; init; }
    public long? TakeProfitHitAt { get; init; }
    public long CreatedAt { get; init; }
    public int EntryHitAfterMinutes { get; init; }
    public int MaxProfitHitAfterMinutes { get; init; }
    public int StopLossHitAfterMinutes { get; init; }
}
```

> All `DateTime` fields are serialized as **Unix timestamp milliseconds** (`long`) using the `ToUnixTimestampMilliseconds()` extension from `Lib.Application.Extensions`.
>
> `EntryHitAfterMinutes`, `MaxProfitHitAfterMinutes`, and `StopLossHitAfterMinutes` are **persisted columns** on the `Signals` table. They are read directly from the database — no in-memory computation. They default to `-1` until the corresponding event has occurred (`-1` means "not yet hit").

### Business Rules

1. `From` and `To` dates are **required**. Validation rejects requests missing either.
2. `From` must be earlier than or equal to `To`.
3. `Interval` is optional. When provided, must match one of the known interval strings (`1min`, `5min`, `15min`, `30min`, `1hour`, `4hour`, `1day`).
4. `SignalType` is optional. When provided, must be a valid `SignalType` enum value (`Long` or `Short`).
5. Filter on `DetectedAt` uses inclusive range: `From <= DetectedAt <= To`.
6. Sorting is controlled by the optional `SortBy` and `SortOrder` parameters:
   - `SortBy` accepted values: `createdAt` (default), `entryHitAfterMinutes`, `maxProfitHitAfterMinutes`, `stopLossHitAfterMinutes`
   - `SortOrder` accepted values: `asc`, `desc` (case-insensitive). Default when omitted: `desc`
   - When `SortBy` is `null`, sort falls back to `CreatedAt DESC` (or ASC if `SortOrder=asc`)
   - `"entryHitAfterMinutes"`: sorts by the persisted `EntryHitAfterMinutes` column — `-1` (not yet hit) sorts first in ASC, last in DESC
   - `"maxProfitHitAfterMinutes"`: sorts by the persisted `MaxProfitHitAfterMinutes` column — `-1` sorts first in ASC, last in DESC
   - `"stopLossHitAfterMinutes"`: sorts by the persisted `StopLossHitAfterMinutes` column — `-1` sorts first in ASC, last in DESC
7. Pagination defaults: `PageNumber = 1`, `PageSize = 20`.

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
    "entryHitAfterMinutes" => descending
        ? query.OrderByDescending(s => s.EntryHitAfterMinutes)
        : query.OrderBy(s => s.EntryHitAfterMinutes),
    "maxProfitHitAfterMinutes" => descending
        ? query.OrderByDescending(s => s.MaxProfitHitAfterMinutes)
        : query.OrderBy(s => s.MaxProfitHitAfterMinutes),
    _ => descending
        ? query.OrderByDescending(s => s.CreatedAt)
        : query.OrderBy(s => s.CreatedAt),
};
```

DateTime-to-timestamp conversion is performed in-memory after the DB fetch (EF Core cannot translate `ToUnixTimestampMilliseconds()` to SQL). Sorting is done at the SQL level on persisted columns so pagination remains correct. `EntryHitAfterMinutes` and `MaxProfitHitAfterMinutes` are read directly from the database.

```csharp
var raw = await orderedQuery
    .Skip((request.PageNumber - 1) * request.PageSize)
    .Take(request.PageSize)
    .Select(s => new { /* all columns including EntryHitAfterMinutes, MaxProfitHitAfterMinutes */ })
    .ToListAsync(cancellationToken);

var items = raw.Select(s => new SignalDto
{
    DetectedAt = s.DetectedAt.ToUnixTimestampMilliseconds(),
    // ...
    EntryHitAfterMinutes = s.EntryHitAfterMinutes,
    MaxProfitHitAfterMinutes = s.MaxProfitHitAfterMinutes,
}).ToList();
```

---

## Backend Architecture

### Application Layer

**Files in `Cex.Application/Signal/Queries/GetSignals/`:**

#### `GetSignalsQueryHandler.cs` (query record + handler colocated)

```csharp
using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Extensions;
using Lib.Application.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Signals.Queries.GetSignals;

public record GetSignalsQuery(
    DateTime From,
    DateTime To,
    string? Interval = null,
    SignalType? SignalType = null,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortOrder = null) : IRequest<PaginatedList<SignalDto>>;

public class GetSignalsQueryHandler(ICexDbContext dbContext)
    : IRequestHandler<GetSignalsQuery, PaginatedList<SignalDto>>
{
    public async Task<PaginatedList<SignalDto>> Handle(
        GetSignalsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Signals
            .AsNoTracking()
            .Where(s => s.DetectedAt >= request.From && s.DetectedAt <= request.To);

        if (!string.IsNullOrEmpty(request.Interval))
            query = query.Where(s => s.Interval == request.Interval);

        if (request.SignalType.HasValue)
            query = query.Where(s => s.SignalType == request.SignalType.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = !string.Equals(request.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        var orderedQuery = request.SortBy switch
        {
            "entryHitAfterMinutes" => descending
                ? query.OrderByDescending(s => s.EntryHitAfterMinutes)
                : query.OrderBy(s => s.EntryHitAfterMinutes),
            "maxProfitHitAfterMinutes" => descending
                ? query.OrderByDescending(s => s.MaxProfitHitAfterMinutes)
                : query.OrderBy(s => s.MaxProfitHitAfterMinutes),
            _ => descending
                ? query.OrderByDescending(s => s.CreatedAt)
                : query.OrderBy(s => s.CreatedAt),
        };

        var raw = await orderedQuery
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new
            {
                s.Id,
                s.Symbol,
                s.Interval,
                s.SignalType,
                s.DetectedAt,
                s.RsiValue,
                s.PreviousRsiValue,
                s.EntryPrice,
                s.StopLoss,
                s.TakeProfit,
                s.Leverage,
                s.MaxProfit,
                s.MaxProfitHitAt,
                s.EntryHitAt,
                s.StopLossHitAt,
                s.TakeProfitHitAt,
                s.CreatedAt,
                s.EntryHitAfterMinutes,
                s.MaxProfitHitAfterMinutes,
            })
            .ToListAsync(cancellationToken);

        var items = raw.Select(s => new SignalDto
        {
            Id = s.Id,
            Symbol = s.Symbol,
            Interval = s.Interval,
            SignalType = s.SignalType.ToString(),
            DetectedAt = s.DetectedAt.ToUnixTimestampMilliseconds(),
            RsiValue = s.RsiValue,
            PreviousRsiValue = s.PreviousRsiValue,
            EntryPrice = s.EntryPrice,
            StopLoss = s.StopLoss,
            TakeProfit = s.TakeProfit,
            Leverage = s.Leverage,
            MaxProfit = s.MaxProfit,
            MaxProfitHitAt = s.MaxProfitHitAt?.ToUnixTimestampMilliseconds(),
            EntryHitAt = s.EntryHitAt?.ToUnixTimestampMilliseconds(),
            StopLossHitAt = s.StopLossHitAt?.ToUnixTimestampMilliseconds(),
            TakeProfitHitAt = s.TakeProfitHitAt?.ToUnixTimestampMilliseconds(),
            CreatedAt = s.CreatedAt.ToUnixTimestampMilliseconds(),
            EntryHitAfterMinutes = s.EntryHitAfterMinutes,
            MaxProfitHitAfterMinutes = s.MaxProfitHitAfterMinutes,
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PaginatedList<SignalDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            TotalPages = totalPages,
            TotalCount = totalCount,
        };
    }
}
```

#### `GetSignalsQueryValidator.cs`

```csharp
using FluentValidation;

namespace Cex.Application.Signals.Queries.GetSignals;

public class GetSignalsQueryValidator : AbstractValidator<GetSignalsQuery>
{
    private static readonly HashSet<string> ValidIntervals =
        ["1min", "5min", "15min", "30min", "1hour", "4hour", "1day"];

    private static readonly HashSet<string> ValidSortByValues =
        ["createdAt", "entryHitAfterMinutes", "maxProfitHitAfterMinutes"];

    public GetSignalsQueryValidator()
    {
        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From date is required");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("To date is required")
            .GreaterThanOrEqualTo(x => x.From)
            .WithMessage("To date must be greater than or equal to From date");

        RuleFor(x => x.Interval)
            .Must(i => ValidIntervals.Contains(i!))
            .When(x => !string.IsNullOrEmpty(x.Interval))
            .WithMessage("Interval must be one of: 1min, 5min, 15min, 30min, 1hour, 4hour, 1day");

        RuleFor(x => x.SignalType)
            .IsInEnum()
            .When(x => x.SignalType.HasValue)
            .WithMessage("SignalType must be Long or Short");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.SortBy)
            .Must(s => ValidSortByValues.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.SortBy))
            .WithMessage("SortBy must be one of: createdAt, entryHitAfterMinutes, maxProfitHitAfterMinutes");

        RuleFor(x => x.SortOrder)
            .Must(s => s!.Equals("asc", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrEmpty(x.SortOrder))
            .WithMessage("SortOrder must be 'asc' or 'desc'");
    }
}
```

#### `SignalDto.cs`

As defined in the DTOs section above.

### API Layer

**File: `src/WebAPI/Controllers/SignalsController.cs`**

```csharp
using Cex.Application.Signals.Queries.GetSignals;
using Cex.Domain.Enums;
using Lib.Application.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Authorize]
[Route("api/signals")]
[ApiController]
public class SignalsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Retrieves paginated signals filtered by date range, interval, and signal type.
    /// </summary>
    /// <param name="from">Start of the date range (required, inclusive)</param>
    /// <param name="to">End of the date range (required, inclusive)</param>
    /// <param name="interval">Optional interval filter (e.g., 1min, 5min, 15min, 30min, 1hour, 4hour, 1day)</param>
    /// <param name="signalType">Optional signal type filter (Long or Short)</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="sortBy">Sort field: createdAt (default), entryHitAfterMinutes, maxProfitHitAfterMinutes</param>
    /// <param name="sortOrder">Sort direction: asc or desc (default: desc)</param>
    /// <returns>Paginated list of signals with entryHitAfterMinutes and maxProfitHitAfterMinutes computed fields</returns>
    [HttpGet]
    public Task<PaginatedList<SignalDto>> GetSignals(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? interval = null,
        [FromQuery] SignalType? signalType = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
        => sender.Send(new GetSignalsQuery(from, to, interval, signalType, pageNumber, pageSize, sortBy, sortOrder));
}
```

**Endpoint:**

| Method | Route | Request | Response |
|---|---|---|---|
| GET | `/api/signals?from=...&to=...` | `from` (required), `to` (required), `interval` (optional), `signalType` (optional), `pageNumber` (default 1), `pageSize` (default 20), `sortBy` (optional), `sortOrder` (optional, default `desc`) | `PaginatedList<SignalDto>` |

---

## Frontend Architecture

**Files in `src/WebUI.React/src/features/signal/signals/`:**

### Column Definition (`index.tsx`)

Sortable columns are marked with `sortable: true`. The column `id` is used directly as the `sortBy` value sent to the API — no separate sort key mapping is needed.

```typescript
const COLUMNS: Column[] = [
  { id: 'detectedAt', label: 'Detected At' },
  { id: 'type', label: 'Type', align: 'center' },
  { id: 'interval', label: 'Interval', align: 'center' },
  { id: 'entryPrice', label: 'Entry Price', align: 'right' },
  { id: 'stopLoss', label: 'Stop Loss', align: 'right' },
  { id: 'maxProfit', label: 'Max Profit %', align: 'right' },
  { id: 'maxProfitHitAfterMinutes', label: 'Max Profit Hit', align: 'right', sortable: true },
  { id: 'entryHitAfterMinutes', label: 'Entry Hit', align: 'center', sortable: true },
  { id: 'createdAt', label: 'Created At', sortable: true },
  { id: 'stopLossHitAfterMinutes', label: 'SL Hit', align: 'center', sortable: true },
];
```

### Duration Display

`entryHitAfterMinutes` and `maxProfitHitAfterMinutes` are read directly from the DTO (no client-side date arithmetic). Values are converted from minutes to milliseconds for `formatDuration`. A guard of `>= 0` handles the `-1` sentinel:

```typescript
entryHit: item.entryHitAfterMinutes >= 0 && entryHitDate ? (
  <Tooltip title={entryHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
    <Typography variant="body2" color="success.main" sx={{ cursor: 'default' }}>
      {formatDuration(item.entryHitAfterMinutes * 60_000)}
    </Typography>
  </Tooltip>
) : '—',

maxProfitHit: item.maxProfitHitAfterMinutes >= 0 && maxProfitHitDate ? (
  <Tooltip title={maxProfitHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
    <Typography variant="body2" color="success.main" sx={{ cursor: 'default' }}>
      {formatDuration(item.maxProfitHitAfterMinutes * 60_000)}
    </Typography>
  </Tooltip>
) : '—',

stopLossHitAfterMinutes: item.stopLossHitAfterMinutes >= 0 && stopLossHitDate ? (
  <Tooltip title={stopLossHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
    <Typography variant="body2" color="error.main" sx={{ cursor: 'default' }}>
      {formatDuration(item.stopLossHitAfterMinutes * 60_000)}
    </Typography>
  </Tooltip>
) : '—',
```

### `Column` Type (`src/WebUI.React/src/components/table/types.ts`)

```typescript
export type Column = {
  id: string;
  label: string;
  align?: 'inherit' | 'left' | 'center' | 'right' | 'justify';
  sortable?: boolean;
};

export type TableDataProps = {
  // ...
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  onSortChange?: (sortBy: string, sortOrder: 'asc' | 'desc') => void;
};
```

`sortBy` and `sortOrder` are the consistent names across all sort-related props and callbacks — used in `TableDataProps`, the `onSortChange` callback, and the internal click handler (`colId` names the column being clicked). `TableData` uses `col.id` as the sort value when `col.sortable` is `true` — the `active` state and click handler both reference `col.id` directly:

```tsx
const handleHeaderClick = (colId: string) => {
  if (!onSortChange) return;
  if (sortBy === colId) {
    sortOrder === 'asc' ? onSortChange(colId, 'desc') : onSortChange('', 'desc');
  } else {
    onSortChange(colId, 'asc');
  }
};

// ...
{col.sortable ? (
  <TableSortLabel
    active={sortBy === col.id}
    direction={sortBy === col.id ? (sortOrder ?? 'asc') : 'asc'}
    onClick={() => handleHeaderClick(col.id)}
    sx={{ '& .MuiTableSortLabel-icon': { opacity: sortBy === col.id ? 1 : 0.3 } }}
  >
    {col.label}
  </TableSortLabel>
) : col.label}
```

Sort cycle on click: inactive → ASC → DESC → reset. On reset, `onSortChange('', 'desc')` is called. The parent's `handleSortChange` coerces the empty string back to the default:

```tsx
const handleSortChange = (sortBy: string, sortOrder: 'asc' | 'desc') => {
  setSignalQuery((prev) => ({
    ...prev,
    sortBy: sortBy || 'createdAt',
    sortOrder: sortBy ? sortOrder : 'desc',
    pageNumber: 1,
  }));
};
```

This ensures the `createdAt` column header re-activates with `DESC` after a reset, matching the actual backend default. Dimmed arrow (opacity 0.3) on inactive sortable columns provides a visual affordance. Default sort on page load is `createdAt DESC`, reflected in the column header immediately.

### `useGetSignals` hook (`src/WebUI.React/src/features/signal/signals/hooks/use-get-signals.ts`)

`sortBy` and `sortOrder` are sent independently — `sortOrder` is included whenever it is set, not only when `sortBy` is also set:

```typescript
if (params.sortBy) url.searchParams.set('sortBy', params.sortBy);
if (params.sortOrder) url.searchParams.set('sortOrder', params.sortOrder);
```

`staleTime: 30_000` prevents unnecessary background refetches on window focus/mount — signal data does not change in real time from the user's perspective.

### `TableData` row key (`src/WebUI.React/src/components/table/index.tsx`)

Rows use a stable key derived from the data's `id` field, with `rowIndex` as a fallback for generic use:

```tsx
{data?.map((row, rowIndex) => (
  <TableRow key={String(row['id'] ?? rowIndex)}>
```

### `signalQuery` initial state (`index.tsx`)

Uses the lazy initializer form of `useState` so the date computations run only once (on mount), not on every render:

```tsx
const [signalQuery, setSignalQuery] = useState(() => ({
  from: new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1).toISOString(),
  to: new Date().toISOString(),
  // ...
}));
```

---

## Performance Considerations

- **Existing index**: `IX_Signals_Symbol_Interval_DetectedAt` (unique) partially covers the query when `Interval` is provided.
- **Consider adding**: A non-clustered index on `DetectedAt DESC` to optimize date-range-only queries without interval filter. Evaluate after observing query plans with production data volumes.
- **Sort indexes**: `IX_Signals_EntryHitAfterMinutes`, `IX_Signals_MaxProfitHitAfterMinutes`, and `IX_Signals_StopLossHitAfterMinutes` added to support efficient sorting on the persisted duration columns.
- **`.AsNoTracking()`**: Used to avoid change-tracker overhead on read-only queries.
- **Two-step fetch**: Raw DB projection to anonymous type, then in-memory mapping to `SignalDto`. Required because `ToUnixTimestampMilliseconds()` cannot be translated to SQL by EF Core.

---

## Error Handling

| Scenario | Handling |
|---|---|
| Missing `from` or `to` | FluentValidation returns 400 with field-level error |
| `from` > `to` | FluentValidation returns 400: "To date must be greater than or equal to From date" |
| Invalid `interval` value | FluentValidation returns 400 with allowed values list |
| Invalid `signalType` value | FluentValidation returns 400: "SignalType must be Long or Short" |
| Invalid `sortBy` value | FluentValidation returns 400: "SortBy must be one of: createdAt, entryHitAfterMinutes, maxProfitHitAfterMinutes, stopLossHitAfterMinutes" |
| Invalid `sortOrder` value | FluentValidation returns 400: "SortOrder must be 'asc' or 'desc'" |
| No matching records | Returns 200 with empty `Items` list and `TotalCount = 0` |

---

## Implementation Checklist

### Application Layer
- [x] Create folder `src/Cex/Cex.Application/Signal/Queries/GetSignals/`
- [x] Create `SignalDto.cs`
- [x] Create `GetSignalsQuery.cs` (query record)
- [x] Create `GetSignalsQueryHandler.cs` (handler)
- [x] Create `GetSignalsQueryValidator.cs`

### Domain & Infrastructure
- [x] Add `EntryHitAfterMinutes` and `MaxProfitHitAfterMinutes` to `Signal` entity
- [x] Add indexes on both columns in `SignalConfiguration`
- [x] Migration `AddSignalDurationColumns` with data backfill
- [x] `CheckSignalEntry` sets `EntryHitAfterMinutes` on hit
- [x] `CheckSignalMaxProfit` sets `MaxProfitHitAfterMinutes` on new high
- [x] `CheckSignalStopLoss` sets `StopLossHitAfterMinutes` on hit

### API Layer
- [x] Create `src/WebAPI/Controllers/SignalsController.cs`

### Frontend
- [x] Regenerate TypeScript API client: `npm run generate-api-client`
- [x] React feature for signals listing (`src/WebUI.React/src/features/signal/signals/`)
- [x] Column-click sorting with asc/desc toggle — `sortable: true` on createdAt, entryHitAfterMinutes, maxProfitHitAfterMinutes columns; `col.id` used as sort key
- [x] Default sort state (createdAt DESC) reflected in column header on page load

### Testing
- [ ] Validate date-range filtering returns correct records
- [ ] Validate optional `Interval` filter narrows results
- [ ] Validate optional `SignalType` filter narrows results
- [ ] Validate pagination (page boundary, total count)
- [ ] Validate validator rejects missing dates, invalid interval, `from > to`

---

## Technical Notes

- **`SignalType` is stored as a string** in the database via `HasConversion<string>()`. EF Core handles the conversion transparently when filtering with the enum value.
- **`Interval` is a plain string**, not an enum in the domain model. The validator enforces known values to prevent misuse, but the handler compares strings directly.
- **No user-scoping**: Unlike `TradeHistory`, signals are system-wide (no `UserId` column). The `[Authorize]` attribute ensures only authenticated users can access the endpoint, but results are not filtered per user.
- **`SignalType` in the DTO is a string** (via `.ToString()`) for frontend consumption, avoiding enum serialization issues.
- **All timestamps in the DTO are Unix milliseconds (`long`)** to simplify frontend date handling and avoid timezone serialization issues.
- **Query record and handler are colocated** in `GetSignalsQueryHandler.cs`, following the project convention for command/query files.
- **`EntryHitAfterMinutes`, `MaxProfitHitAfterMinutes`, and `StopLossHitAfterMinutes` are persisted columns**, not computed at query time. This avoids SQL Server-specific EF functions (`DateDiffMinute`) in the Application layer and makes sorting trivially translatable by any EF Core provider. All three are non-nullable `int` with a default of `-1` (sentinel for "not yet hit"). Written by `CheckSignalEntry`, `CheckSignalMaxProfit`, and `CheckSignalStopLoss` respectively; backfilled via migration using `ISNULL(DATEDIFF(...), -1)` for existing rows.

---

## Related Features

- **FindSignal** (`Signal/Commands/FindSignal/`) — creates `Signal` entries that this query reads
- **CheckSignalEntry** (`Signal/Commands/CheckSignalEntry/`) — sets `EntryHitAt` and `EntryHitAfterMinutes` when entry price is hit
- **CheckSignalStopLoss** (`Signal/Commands/CheckSignalStopLoss/`) — updates `StopLossHitAt` on records
- **CheckSignalMaxProfit** (`Signal/Commands/CheckSignalMaxProfit/`) — updates `MaxProfit`, `MaxProfitHitAt`, and `MaxProfitHitAfterMinutes` whenever a new high is recorded
- **CheckSignalStopLoss** (`Signal/Commands/CheckSignalStopLoss/`) — sets `StopLossHitAt` and `StopLossHitAfterMinutes` when stop loss price is hit

---

## Future

- Add export to CSV functionality
- Add signal performance summary statistics (win rate, average profit, etc.)
- Add Symbol filter when multi-symbol support is added
