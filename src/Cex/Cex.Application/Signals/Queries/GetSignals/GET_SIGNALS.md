# GetSignals

## Overview

Query all `Signal` entries with required date-range filtering and optional `Interval` / `SignalType` filters. This is the first read endpoint for the Signal module, enabling users to review detected signals over a given time period. Results are paginated and sorted by `DetectedAt` descending (most recent first).

**Module Location**: `src/Cex/Cex.Application/Signal/Queries/GetSignals/`
**Scope**: All signals (system-wide, not user-scoped). BTCUSDT only in v1.

---

## Data Model

### Entity: Signal (Existing)

No schema changes required. The query reads the existing `Signal` table.

Relevant columns for filtering:

| Column | Type | Nullable | Notes |
|---|---|---|---|
| DetectedAt | datetime2(0) | No | Primary filter: `FROM <= DetectedAt <= TO` |
| Interval | nvarchar(20) | No | Optional filter. Stored values: `1min`, `5min`, `15min`, `30min`, `1hour`, `4hour`, `1day` |
| SignalType | nvarchar(max) | No | Optional filter. Stored values: `Long`, `Short` (string conversion of enum) |

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
}
```

> All `DateTime` fields are serialized as **Unix timestamp milliseconds** (`long`) using the `ToUnixTimestampMilliseconds()` extension from `Lib.Application.Extensions`.

### Business Rules

1. `From` and `To` dates are **required**. Validation rejects requests missing either.
2. `From` must be earlier than or equal to `To`.
3. `Interval` is optional. When provided, must match one of the known interval strings (`1min`, `5min`, `15min`, `30min`, `1hour`, `4hour`, `1day`).
4. `SignalType` is optional. When provided, must be a valid `SignalType` enum value (`Long` or `Short`).
5. Filter on `DetectedAt` uses inclusive range: `From <= DetectedAt <= To`.
6. Results are ordered by `DetectedAt` descending.
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

query = query.OrderByDescending(s => s.DetectedAt);
```

DateTime-to-timestamp conversion is performed in-memory after the DB fetch (EF Core cannot translate `ToUnixTimestampMilliseconds()` to SQL):

```csharp
var raw = await query
    .OrderByDescending(s => s.DetectedAt)
    .Skip((request.PageNumber - 1) * request.PageSize)
    .Take(request.PageSize)
    .Select(s => new { /* all columns */ })
    .ToListAsync(cancellationToken);

var items = raw.Select(s => new SignalDto
{
    DetectedAt = s.DetectedAt.ToUnixTimestampMilliseconds(),
    // ...
}).ToList();
```

---

## Backend Architecture

### Application Layer

**Files in `Cex.Application/Signal/Queries/GetSignals/`:**

#### `GetSignalsQuery.cs` (query record only)

```csharp
using Cex.Domain.Enums;
using Lib.Application.Models;
using MediatR;

namespace Cex.Application.Signals.Queries.GetSignals;

public record GetSignalsQuery(
    DateTime From,
    DateTime To,
    string? Interval = null,
    SignalType? SignalType = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PaginatedList<SignalDto>>;
```

#### `GetSignalsQueryHandler.cs`

```csharp
using Cex.Application.Common.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Signals.Queries.GetSignals;

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

        var raw = await query
            .OrderByDescending(s => s.DetectedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new
            {
                s.Id, s.Symbol, s.Interval, s.SignalType, s.DetectedAt,
                s.RsiValue, s.PreviousRsiValue, s.EntryPrice, s.StopLoss,
                s.TakeProfit, s.Leverage, s.MaxProfit, s.MaxProfitHitAt,
                s.EntryHitAt, s.StopLossHitAt, s.TakeProfitHitAt, s.CreatedAt,
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
    [HttpGet]
    public Task<PaginatedList<SignalDto>> GetSignals(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? interval = null,
        [FromQuery] SignalType? signalType = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => sender.Send(new GetSignalsQuery(from, to, interval, signalType, pageNumber, pageSize));
}
```

**Endpoint:**

| Method | Route | Request | Response |
|---|---|---|---|
| GET | `/api/signals?from=...&to=...` | `from` (required), `to` (required), `interval` (optional), `signalType` (optional), `pageNumber` (default 1), `pageSize` (default 20) | `PaginatedList<SignalDto>` |

---

## Performance Considerations

- **Existing index**: `IX_Signals_Symbol_Interval_DetectedAt` (unique) partially covers the query when `Interval` is provided.
- **Consider adding**: A non-clustered index on `DetectedAt DESC` to optimize date-range-only queries without interval filter. Evaluate after observing query plans with production data volumes.
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
| No matching records | Returns 200 with empty `Items` list and `TotalCount = 0` |

---

## Implementation Checklist

### Application Layer
- [x] Create folder `src/Cex/Cex.Application/Signal/Queries/GetSignals/`
- [x] Create `SignalDto.cs`
- [x] Create `GetSignalsQuery.cs` (query record)
- [x] Create `GetSignalsQueryHandler.cs` (handler)
- [x] Create `GetSignalsQueryValidator.cs`

### API Layer
- [x] Create `src/WebAPI/Controllers/SignalsController.cs`

### Frontend
- [ ] Regenerate TypeScript API client: `npm run generate-api-client`
- [ ] Create React feature for signals listing (future scope)

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
- **Handler is in a separate file** (`GetSignalsQueryHandler.cs`) from the query record (`GetSignalsQuery.cs`).

---

## Related Features

- **FindSignal** (`Signal/Commands/FindSignal/`) — creates `Signal` entries that this query reads
- **CheckSignalEntry** (`Signal/Commands/CheckSignalEntry/`) — updates `EntryHitAt` on records
- **CheckSignalStopLoss** (`Signal/Commands/CheckSignalStopLoss/`) — updates `StopLossHitAt` on records
- **CheckSignalMaxProfit** (`Signal/Commands/CheckSignalMaxProfit/`) — updates `MaxProfit` / `MaxProfitHitAt` on records

---

## Future

- Add frontend UI for browsing and filtering signals
- Add export to CSV functionality
- Add signal performance summary statistics (win rate, average profit, etc.)
- Add Symbol filter when multi-symbol support is added
