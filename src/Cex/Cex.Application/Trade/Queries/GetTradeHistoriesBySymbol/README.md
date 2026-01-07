# Get Trade Histories By Symbol Feature

## Table of Contents
- [Overview](#overview)
- [Data Model](#data-model)
  - [Entity: TradeHistory (Existing)](#entity-tradehistory-existing)
  - [Business Rules](#business-rules)
- [Backend Architecture](#backend-architecture)
  - [Domain Layer](#domain-layer)
  - [Infrastructure Layer](#infrastructure-layer)
  - [Application Layer](#application-layer)
  - [Algorithm](#algorithm)
  - [API Layer](#api-layer)
- [Performance Considerations](#performance-considerations)
- [Error Handling](#error-handling)
- [Implementation Checklist](#implementation-checklist)
  - [Backend](#backend)
  - [Testing](#testing)
  - [Frontend Integration](#frontend-integration)
- [Technical Notes](#technical-notes)
- [Database Migration](#database-migration)
- [Related Features](#related-features)
- [Future Enhancements](#future-enhancements)

## Overview
Retrieves paginated trade history records from the local database for a specific cryptocurrency symbol. Returns trades sorted by most recent first with pagination support.

**Module Location**: `Cex.Application/Trade/Queries/GetTradeHistoriesBySymbol/`

**Version 1 Scope**: Query user's own trade history only (user-scoped)

## Data Model

### Entity: TradeHistory (Existing)
```csharp
public class TradeHistory
{
    public string UserId { get; set; }
    public string Symbol { get; set; }
    public long TradeId { get; set; }
    public string OrderId { get; set; }
    public string CounterOrderId { get; set; }
    public string Side { get; set; }          // Buy/Sell
    public string Liquidity { get; set; }
    public bool ForceTaker { get; set; }
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public decimal Funds { get; set; }
    public decimal Fee { get; set; }
    public decimal FeeRate { get; set; }
    public string FeeCurrency { get; set; }
    public string Stop { get; set; }
    public string TradeType { get; set; }
    public string Type { get; set; }
    public DateTime TradedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Composite Key**: `(UserId, TradeId)`  
**Table Name**: `TradeHistories`

**Note**: Entity already exists in `Cex.Domain/Entities/TradeHistory.cs`

### Business Rules
1. **User Scoping**: Only return trades belonging to current authenticated user
2. **Symbol Filter**: Required parameter, exact match (e.g., "BTC-USDT")
3. **Sorting**: Order by TradedAt descending (most recent first)
4. **Pagination**: Default page size = 20, max page size = 100
5. **Page Numbers**: 1-based indexing (page 1 is first page)

## Backend Architecture

### Domain Layer
- `Cex.Domain/Entities/TradeHistory.cs` - Existing entity (already defined)

### Infrastructure Layer
- `Cex.Infrastructure/Data/Configurations/TradeHistoryConfiguration.cs` - Already exists
  - Composite key: `builder.HasKey(x => new { x.UserId, x.TradeId })`
  - Decimal precision: 13,6 for Price, Size, Funds, Fee, FeeRate

### Application Layer
Location: `Cex.Application/Trade/Queries/GetTradeHistoriesBySymbol/`

**Query**:
- `Queries/GetTradeHistoriesBySymbol/GetTradeHistoriesBySymbolQuery.cs`
  ```csharp
  public record GetTradeHistoriesBySymbolQuery(
      string Symbol,
      int PageNumber = 1,
      int PageSize = 20
  ) : IRequest<PaginatedList<TradeHistoryDto>>;
  ```

**DTOs**:
- `TradeHistoryDto` - Data transfer object for TradeHistory entity
  ```csharp
  public record TradeHistoryDto
  {
      public string Symbol { get; init; }
      public long TradeId { get; init; }
      public string OrderId { get; init; }
      public string Side { get; init; }
      public decimal Price { get; init; }
      public decimal Size { get; init; }
      public decimal Funds { get; init; }
      public decimal Fee { get; init; }
      public string FeeCurrency { get; init; }
      public DateTime TradedAt { get; init; }
      
      // Optional: Additional computed properties
      public decimal Total => Funds + Fee;  // Total cost including fee
  }
  ```

- `PaginatedList<T>` - Generic pagination wrapper (from Lib.Application)
  ```csharp
  public class PaginatedList<T>
  {
      public List<T> Items { get; set; }
      public int PageNumber { get; set; }
      public int TotalPages { get; set; }
      public int TotalCount { get; set; }
  }
  ```

**Dependencies**:
- `ICexDbContext` - Database context
- `ICurrentUser` - User context for user scoping
- `IMapper` (optional) - AutoMapper for entity to DTO mapping

**Validation**:
- `GetTradeHistoriesBySymbolQueryValidator` - FluentValidation
  ```csharp
  public class GetTradeHistoriesBySymbolQueryValidator 
      : AbstractValidator<GetTradeHistoriesBySymbolQuery>
  {
      public GetTradeHistoriesBySymbolQueryValidator()
      {
          RuleFor(x => x.Symbol)
              .NotEmpty()
              .WithMessage("Symbol is required")
              .MaximumLength(20)
              .WithMessage("Symbol cannot exceed 20 characters");
          
          RuleFor(x => x.PageNumber)
              .GreaterThanOrEqualTo(1)
              .WithMessage("Page number must be at least 1");
          
          RuleFor(x => x.PageSize)
              .GreaterThanOrEqualTo(1)
              .WithMessage("Page size must be at least 1")
              .LessThanOrEqualTo(100)
              .WithMessage("Page size cannot exceed 100");
      }
  }
  ```

### Algorithm

#### Step 1: Validate Input
```csharp
// FluentValidation handles this automatically via MediatR pipeline
// - Symbol: required, max 20 chars
// - PageNumber: >= 1
// - PageSize: 1-100
```

#### Step 2: Query Database with Pagination
```csharp
public async Task<PaginatedList<TradeHistoryDto>> Handle(
    GetTradeHistoriesBySymbolQuery request,
    CancellationToken cancellationToken)
{
    var userId = currentUser.Id;
    
    // Build base query with filters
    var query = cexDbContext.TradeHistories
        .Where(x => x.UserId == userId && x.Symbol == request.Symbol)
        .OrderByDescending(x => x.TradedAt);
    
    // Get total count for pagination
    var totalCount = await query.CountAsync(cancellationToken);
    
    // Apply pagination
    var items = await query
        .Skip((request.PageNumber - 1) * request.PageSize)
        .Take(request.PageSize)
        .Select(x => new TradeHistoryDto
        {
            Symbol = x.Symbol,
            TradeId = x.TradeId,
            OrderId = x.OrderId,
            Side = x.Side,
            Price = x.Price,
            Size = x.Size,
            Funds = x.Funds,
            Fee = x.Fee,
            FeeCurrency = x.FeeCurrency,
            TradedAt = x.TradedAt
        })
        .ToListAsync(cancellationToken);
    
    // Calculate total pages
    var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
    
    return new PaginatedList<TradeHistoryDto>
    {
        Items = items,
        PageNumber = request.PageNumber,
        TotalPages = totalPages,
        TotalCount = totalCount
    };
}
```

**Query Optimization**:
- **Composite Index**: Use existing composite key (UserId, TradeId)
- **Additional Index**: Consider adding index on (UserId, Symbol, TradedAt) for faster filtering and sorting
- **Projection**: Select only needed fields in DTO to reduce data transfer
- **No Tracking**: Use `.AsNoTracking()` for read-only queries (performance boost)

#### Step 3: Return Result
```csharp
// Result automatically serialized to JSON by API controller
// Frontend receives:
{
  "items": [...],
  "pageNumber": 1,
  "totalPages": 5,
  "totalCount": 87
}
```

### API Layer
**Controller**: `WebAPI/Controllers/TradeController.cs`

**Endpoints**:
- `GET /api/trade/history/{symbol}` → Returns paginated trade history
  - Route parameter: `symbol` (e.g., "BTC-USDT")
  - Query parameters: `pageNumber` (default: 1), `pageSize` (default: 20)
  - Returns: `PaginatedList<TradeHistoryDto>`
  - Authorization: Required (user must be authenticated)

**Example Request**:
```http
GET /api/trade/history/BTC-USDT?pageNumber=1&pageSize=20
Authorization: Bearer {token}
```

**Example Response**:
```json
{
  "items": [
    {
      "symbol": "BTC-USDT",
      "tradeId": 123456789,
      "orderId": "abc123",
      "side": "buy",
      "price": 43250.50,
      "size": 0.0123,
      "funds": 531.78,
      "fee": 0.53,
      "feeCurrency": "USDT",
      "tradedAt": "2026-01-05T10:30:00Z",
      "total": 532.31
    }
  ],
  "pageNumber": 1,
  "totalPages": 5,
  "totalCount": 87
}
```

## Performance Considerations

1. **Database Index**: Add composite index on (UserId, Symbol, TradedAt DESC) for optimal query performance
   ```sql
   CREATE INDEX IX_TradeHistories_UserId_Symbol_TradedAt 
   ON TradeHistories (UserId, Symbol, TradedAt DESC);
   ```

2. **Query Optimization**:
   - Use `.AsNoTracking()` for read-only queries
   - Select only necessary fields in projection
   - Avoid N+1 queries (single query with pagination)

3. **Caching Strategy** (Future):
   - Cache first page results (most frequently accessed)
   - Cache duration: 5 minutes
   - Invalidate on new trades synced

4. **Pagination Limits**:
   - Max page size: 100 (prevents large result sets)
   - Default page size: 20 (balance between data transfer and roundtrips)

## Error Handling

- **Invalid Symbol**: Returns 422 Unprocessable Entity with validation error
- **Invalid Pagination**: Returns 422 Unprocessable Entity (page < 1 or size > 100)
- **Unauthorized**: Returns 401 if user not authenticated
- **No Data**: Returns empty items array with totalCount = 0 (not an error)
- **Database Error**: Returns 500 with generic error message (log detailed error)

## Implementation Checklist

### Backend
- [ ] Check if `DbSet<TradeHistory>` exists in ICexDbContext and CexDbContext
- [ ] Create `GetTradeHistoriesBySymbolQuery` record
- [ ] Create `TradeHistoryDto` record
- [ ] Check if `PaginatedList<T>` exists in Lib.Application (or create it)
- [ ] Implement `GetTradeHistoriesBySymbolQueryHandler`
- [ ] Implement `GetTradeHistoriesBySymbolQueryValidator`
- [ ] Add endpoint to `TradeController` (GET /api/trade/history/{symbol})
- [ ] Add database index: `IX_TradeHistories_UserId_Symbol_TradedAt`
- [ ] Write unit tests for handler logic
- [ ] Write integration tests

### Testing
- [ ] Unit test: Empty result (no trades for symbol)
- [ ] Unit test: Single page result (< page size)
- [ ] Unit test: Multiple pages result
- [ ] Unit test: Last page with partial results
- [ ] Unit test: Validation failures (invalid symbol, page number, page size)
- [ ] Integration test: Query with real data
- [ ] Integration test: User isolation (can't see other users' trades)
- [ ] Manual test: Swagger UI testing
- [ ] Manual test: Performance with large dataset (10k+ trades)

### Frontend Integration
- [ ] Generate TypeScript client: `npm run generate-api-client`
- [ ] Create `useGetTradeHistories` hook
- [ ] Create trade history list component with pagination controls
- [ ] Add loading states and error handling
- [ ] Display buy/sell with color coding (green/red)
- [ ] Format numbers (price, size with proper decimals)
- [ ] Format dates (relative or absolute)
- [ ] Add sorting options (future enhancement)
- [ ] Add date range filters (future enhancement)

## Technical Notes

- **User Scoping**: Always filter by `ICurrentUser.Id` for security
- **Symbol Format**: Case-sensitive, typically uppercase with hyphen (e.g., "BTC-USDT")
- **Sorting**: Default descending by TradedAt (newest first)
- **Read-Only**: Query operation, no database modifications
- **Authorization**: Requires authenticated user (JWT token)
- **DTO Mapping**: Manual mapping in Select() for best performance (avoid AutoMapper in queries)

## Database Migration

If index doesn't exist, create migration:

```bash
cd src/Cex/Cex.Infrastructure
dotnet ef migrations add AddTradeHistorySymbolIndex \
  --startup-project ../../WebAPI/WebAPI.csproj \
  --context CexDbContext
```

**Migration Code**:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateIndex(
        name: "IX_TradeHistories_UserId_Symbol_TradedAt",
        table: "TradeHistories",
        columns: new[] { "UserId", "Symbol", "TradedAt" },
        descending: new[] { false, false, true });
}
```

## Related Features

- **SyncTradeHistoryBySymbol**: Command that populates the data this query reads
- **SyncSetting**: Determines which symbols have trade history
- **TradeHistory Entity**: Source data for this query

## Future Enhancements

1. **Advanced Filtering**:
   - Filter by Side (buy/sell only)
   - Filter by date range (from/to dates)
   - Filter by order type (market/limit)

2. **Sorting Options**:
   - Sort by Price (asc/desc)
   - Sort by Size (asc/desc)
   - Sort by Funds (asc/desc)

3. **Aggregations** (separate endpoint):
   - Total buy/sell amounts
   - Average prices
   - Profit/loss calculations

4. **Export**:
   - CSV export for tax reporting
   - Excel export with charts

5. **Real-time Updates**:
   - WebSocket notifications on new trades
   - SignalR integration
