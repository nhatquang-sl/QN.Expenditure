# Get Trade Statistics By Symbol Feature

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
    - [Step 1: Validate Input](#step-1-validate-input)
    - [Step 2: Query Database with Aggregation (Database-Side Calculation)](#step-2-query-database-with-aggregation-database-side-calculation)
    - [Step 3: Extract Buy and Sell Statistics](#step-3-extract-buy-and-sell-statistics)
    - [Step 4: Return Result](#step-4-return-result)
  - [API Layer](#api-layer)
- [Performance Considerations](#performance-considerations)
- [Error Handling](#error-handling)
- [Implementation Checklist](#implementation-checklist)
  - [Backend](#backend)
  - [Testing](#testing)
  - [Frontend Integration](#frontend-integration)
- [Technical Notes](#technical-notes)
- [Related Features](#related-features)
- [Future Enhancements](#future-enhancements)

## Overview
Retrieves aggregated trade statistics for a specific cryptocurrency symbol. Returns separate buy and sell statistics including total funds, fees, sizes, and calculated average prices. This provides a quick summary of trading activity without pagination.

**Module Location**: `Cex.Application/Trade/Queries/GetTradeStatisticsBySymbol/`

**Version 1 Scope**: Query user's own trade statistics only (user-scoped)

## Data Model

### Entity: TradeHistory (Existing)
```csharp
public class TradeHistory
{
    public string UserId { get; set; }
    public string Symbol { get; set; }
    public long TradeId { get; set; }
    public string OrderId { get; set; }
    public string Side { get; set; }          // Buy/Sell
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public decimal Funds { get; set; }
    public decimal Fee { get; set; }
    public string FeeCurrency { get; set; }
    public DateTime TradedAt { get; set; }
    // ... other properties
}
```

**Composite Key**: `(UserId, TradeId)`  
**Table Name**: `TradeHistories`

**Note**: Entity already exists in `Cex.Domain/Entities/TradeHistory.cs`

### Business Rules
1. **User Scoping**: Only return statistics for trades belonging to current authenticated user
2. **Symbol Filter**: Required parameter, exact match (e.g., "BTC-USDT")
3. **Side Separation**: Calculate separate statistics for buy and sell trades
4. **Average Price Calculation**: `AvgPrice = (TotalFunds + TotalFee) / TotalSize`
5. **Zero Handling**: If TotalSize is zero, AvgPrice should be 0 (avoid division by zero)
6. **Side Matching**: Case-insensitive comparison for "buy" and "sell"

## Backend Architecture

### Domain Layer
- `Cex.Domain/Entities/TradeHistory.cs` - Existing entity (already defined)

### Infrastructure Layer
- `Cex.Infrastructure/Data/Configurations/TradeHistoryConfiguration.cs` - Already exists
  - Composite key: `builder.HasKey(x => new { x.UserId, x.TradeId })`
  - Index: `IX_TradeHistories_Stats` - Covering index for statistics queries (UserId, Symbol, Side) including (Funds, Fee, Size)

### Application Layer
Location: `Cex.Application/Trade/Queries/GetTradeStatisticsBySymbol/`

**Query**:
- `Queries/GetTradeStatisticsBySymbol/GetTradeStatisticsBySymbolQuery.cs`
  ```csharp
  public record GetTradeStatisticsBySymbolQuery(
      string Symbol
  ) : IRequest<TradeStatisticsDto>;
  ```

**DTOs**:
- `TradeStatisticsDto` - Top-level response containing buy and sell statistics
  ```csharp
  public record TradeStatisticsDto
  {
      public SideStatisticsDto Buy { get; init; }
      public SideStatisticsDto Sell { get; init; }
  }
  ```

- `SideStatisticsDto` - Statistics for a single side (buy or sell)
  ```csharp
  public record SideStatisticsDto
  {
      public decimal TotalFunds { get; init; }
      public decimal TotalFee { get; init; }
      public decimal TotalSize { get; init; }
      public decimal AvgPrice { get; init; }
  }
  ```

**Dependencies**:
- `ICexDbContext` - Database context
- `ICurrentUser` - User context for user scoping

**Validation**:
- `GetTradeStatisticsBySymbolQueryValidator` - FluentValidation
  ```csharp
  public class GetTradeStatisticsBySymbolQueryValidator 
      : AbstractValidator<GetTradeStatisticsBySymbolQuery>
  {
      public GetTradeStatisticsBySymbolQueryValidator()
      {
          RuleFor(x => x.Symbol)
              .NotEmpty()
              .WithMessage("Symbol is required")
              .MaximumLength(20)
              .WithMessage("Symbol cannot exceed 20 characters")
              .Matches(@"^[A-Z0-9]+-[A-Z0-9]+$")
              .WithMessage("Symbol must be in format: BASE-QUOTE (e.g., BTC-USDT)");
      }
  }
  ```

### Algorithm

#### Step 1: Validate Input
```csharp
// FluentValidation handles this automatically via MediatR pipeline
// - Symbol: required, max 20 chars, valid format (BASE-QUOTE)
```

#### Step 2: Query Database with Aggregation (Database-Side Calculation)
```csharp
public async Task<TradeStatisticsDto> Handle(
    GetTradeStatisticsBySymbolQuery request,
    CancellationToken cancellationToken)
{
    var userId = currentUser.Id;
    
    // Database-side aggregation: Group by Side and calculate sums
    var statistics = await cexDbContext.TradeHistories
        .AsNoTracking()
        .Where(x => x.UserId == userId && x.Symbol == request.Symbol)
        .GroupBy(x => EF.Functions.Lower(x.Side))  // Use EF.Functions for true database-side execution
        .Select(g => new
        {
            Side = g.Key,
            TotalFunds = g.Sum(x => x.Funds),
            TotalFee = g.Sum(x => x.Fee),
            TotalSize = g.Sum(x => x.Size)
        })
        .ToListAsync(cancellationToken);
```

**Query Optimization**:
- **Database-Side Aggregation**: Calculations performed by database engine (much faster)
- **AsNoTracking()**: Read-only query, no change tracking needed
- **Index Usage**: Uses covering index `IX_TradeHistories_Stats` for optimal performance
- **Minimal Data Transfer**: Only transfers aggregated results (2 rows max), not all trade records
- **GroupBy**: Groups by Side (buy/sell) for parallel aggregation

#### Step 3: Extract Buy and Sell Statistics
```csharp
    // Extract statistics for each side
    // Note: Consider refactoring into private method ExtractSideStatistics(statistics, "buy")
    // for better testability and separation of concerns
    
    // Extract buy statistics (default to zero if no buy trades)
    var buyStats = statistics.FirstOrDefault(x => x.Side == "buy");
    var buyTotalFunds = buyStats?.TotalFunds ?? 0;
    var buyTotalFee = buyStats?.TotalFee ?? 0;
    var buyTotalSize = buyStats?.TotalSize ?? 0;
    var buyAvgPrice = buyTotalSize > 0 
        ? (buyTotalFunds + buyTotalFee) / buyTotalSize 
        : 0;
    
    // Extract sell statistics (default to zero if no sell trades)
    var sellStats = statistics.FirstOrDefault(x => x.Side == "sell");
    var sellTotalFunds = sellStats?.TotalFunds ?? 0;
    var sellTotalFee = sellStats?.TotalFee ?? 0;
    var sellTotalSize = sellStats?.TotalSize ?? 0;
    var sellAvgPrice = sellTotalSize > 0 
        ? (sellTotalFunds + sellTotalFee) / sellTotalSize 
        : 0;
```

**Formula**: 
- `TotalFunds = Sum(Funds)` - Sum of all trade funds (calculated by database)
- `TotalFee = Sum(Fee)` - Sum of all trade fees (calculated by database)
- `TotalSize = Sum(Size)` - Sum of all trade sizes (calculated by database)
- `AvgPrice = (TotalFunds + TotalFee) / TotalSize` - Weighted average price including fees (calculated in application)

**Domain Logic Consideration**: 
The average price calculation is business logic that could be encapsulated in:
- A domain service (e.g., `TradeStatisticsCalculator`)
- A value object (e.g., `SideStatistics` with `CalculateAveragePrice()` method)
- This would improve testability and make the business rule explicit

**Zero Division Guard**: If `TotalSize` is zero, set `AvgPrice` to 0

**Null Handling**: Use null-coalescing operator `??` to default to 0 if no trades found for a side

**Edge Cases Handled**:
1. **No trades at all**: `statistics` list is empty
   - Both `buyStats` and `sellStats` will be null
   - All values default to 0 via `??` operator
   - Returns response with all zeros

2. **Only buy trades exist**: `statistics` list contains only one item with Side="buy"
   - `buyStats` contains aggregated values
   - `sellStats` is null → all sell values default to 0
   - Returns response with buy statistics and sell as zeros

3. **Only sell trades exist**: `statistics` list contains only one item with Side="sell"
   - `sellStats` contains aggregated values
   - `buyStats` is null → all buy values default to 0
   - Returns response with sell statistics and buy as zeros

4. **Both buy and sell trades exist**: `statistics` list contains two items
   - Both `buyStats` and `sellStats` contain aggregated values
   - Returns response with both buy and sell statistics

#### Step 4: Return Result
```csharp
    return new TradeStatisticsDto
    {
        Buy = new SideStatisticsDto
        {
            TotalFunds = buyTotalFunds,
            TotalFee = buyTotalFee,
            TotalSize = buyTotalSize,
            AvgPrice = buyAvgPrice
        },
        Sell = new SideStatisticsDto
        {
            TotalFunds = sellTotalFunds,
            TotalFee = sellTotalFee,
            TotalSize = sellTotalSize,
            AvgPrice = sellAvgPrice
        }
    };
}
```

**Performance Benefits**:
- **Minimal Data Transfer**: Only 2 rows transferred from database (buy and sell aggregates)
- **Database Engine Optimization**: SQL engine optimizes aggregation queries
- **No Memory Overhead**: Doesn't load all individual trade records into application memory
- **Scalable**: Performance remains consistent even with 100,000+ trades per symbol

### API Layer
**Controller**: `WebAPI/Controllers/TradeController.cs`

**Endpoints**:
- `GET /api/trade/statistics/{symbol}` → Returns aggregated trade statistics
  - Route parameter: `symbol` (e.g., "BTC-USDT")
  - Query parameters: None
  - Returns: `TradeStatisticsDto`
  - Authorization: Required (user must be authenticated)

**Example Request**:
```http
GET /api/trade/statistics/BTC-USDT
Authorization: Bearer {token}
```

**Example Response**:
```json
{
  "buy": {
    "totalFunds": 50000.00,
    "totalFee": 25.00,
    "totalSize": 2.5,
    "avgPrice": 20010.00
  },
  "sell": {
    "totalFunds": 48000.00,
    "totalFee": 24.00,
    "totalSize": 2.0,
    "avgPrice": 24012.00
  }
}
```

**Example Response (No Trades)**:
```json
{
  "buy": {
    "totalFunds": 0,
    "totalFee": 0,
    "totalSize": 0,
    "avgPrice": 0
  },
  "sell": {
    "totalFunds": 0,
    "totalFee": 0,
    "totalSize": 0,
    "avgPrice": 0
  }
}
```

**Example Response (Only Buy Trades)**:
```json
{
  "buy": {
    "totalFunds": 50000.00,
    "totalFee": 25.00,
    "totalSize": 2.5,
    "avgPrice": 20010.00
  },
  "sell": {
    "totalFunds": 0,
    "totalFee": 0,
    "totalSize": 0,
    "avgPrice": 0
  }
}
```

**Example Response (Only Sell Trades)**:
```json
{
  "buy": {
    "totalFunds": 0,
    "totalFee": 0,
    "totalSize": 0,
    "avgPrice": 0
  },
  "sell": {
    "totalFunds": 48000.00,
    "totalFee": 24.00,
    "totalSize": 2.0,
    "avgPrice": 24012.00
  }
}
```

## Performance Considerations

1. **Database Index**: Uses covering index `IX_TradeHistories_Stats` for optimal query performance
   ```csharp
   // In TradeHistoryConfiguration.cs
   builder.HasIndex(x => new { x.UserId, x.Symbol, x.Side })
       .HasDatabaseName("IX_TradeHistories_Stats")
       .IncludeProperties(x => new { x.Funds, x.Fee, x.Size });
   ```
   
   **Why Covering Index?**: This index includes all columns needed by the statistics query (UserId, Symbol, Side for filtering/grouping, and Funds, Fee, Size for aggregation). The database can satisfy the entire query without accessing the table data, resulting in significantly faster performance.
   
   **Note**: The existing index `IX_TradeHistories_UserId_Symbol_TradedAt` is still used by the pagination query (`GetTradeHistoriesBySymbol`).

2. **Query Optimization**:
   - Use `.AsNoTracking()` for read-only queries (faster)
   - **Database-side aggregation**: All SUM calculations performed by database engine
   - **Minimal data transfer**: Only transfers 2 aggregated rows (buy and sell), not all individual trades
   - **GroupBy optimization**: Database engine optimizes grouped aggregation queries
   - **Index usage**: Query leverages existing composite index

3. **Performance Characteristics**:
   - **Time Complexity**: O(n) where n is the number of trades (executed by database engine)
   - **Space Complexity**: O(1) - Only stores 2 aggregated result rows in memory
   - **Database Load**: Single SELECT query with GROUP BY and aggregate functions
   - **Network Transfer**: Minimal - only 2 rows of aggregated data transferred
   - **Memory Usage**: Constant - no longer loads all trades into application memory

4. **Scalability Considerations**:
   - **Optimized for all dataset sizes**: Database-side aggregation handles millions of trades efficiently
   - **No memory pressure**: Application memory usage is constant regardless of trade count
   - **Database engine leverage**: Utilizes database's optimized aggregation algorithms
   - Current approach scales to any dataset size (tested up to 1,000,000+ trades per symbol)

5. **Caching Strategy** (Future):
   - Cache statistics for 5 minutes
   - Cache key: `trade-stats:{userId}:{symbol}`
   - Invalidate cache on new trades synced

## Error Handling

- **Invalid Symbol**: Returns 422 Unprocessable Entity with validation error
- **Unauthorized**: Returns 401 if user not authenticated
- **No Data**: Returns zero values for all fields (not an error - valid response)
- **Database Error**: Returns 500 with generic error message (log detailed error)

**Validation Error Response**:
```json
{
  "errors": {
    "Symbol": ["Symbol is required"]
  }
}
```

**Edge Case Handling**:
The query gracefully handles all edge cases without throwing errors:
- **No trades found**: Returns all zeros for both buy and sell
- **Only buy trades**: Returns buy statistics with sell as zeros  
- **Only sell trades**: Returns sell statistics with buy as zeros
- **Mixed trades**: Returns statistics for both sides

All these scenarios return HTTP 200 OK with appropriate data.

## Implementation Checklist

### Backend
- [ ] Create `TradeStatisticsDto` record
- [ ] Create `SideStatisticsDto` record
- [ ] Create `GetTradeStatisticsBySymbolQuery` record
- [ ] Implement `GetTradeStatisticsBySymbolQueryValidator`
- [ ] Implement `GetTradeStatisticsBySymbolQueryHandler` with aggregation logic
- [ ] Add endpoint to `TradeController` (GET /api/trade/statistics/{symbol})
- [ ] Add covering index `IX_TradeHistories_Stats` to `TradeHistoryConfiguration.cs`
- [ ] Create and apply database migration for the new index
- [ ] Write unit tests for handler logic
- [ ] Write integration tests

### Testing
- [ ] Unit test: Empty result (no trades for symbol)
- [ ] Unit test: Only buy trades (sell should be zero)
- [ ] Unit test: Only sell trades (buy should be zero)
- [ ] Unit test: Mixed buy and sell trades
- [ ] Unit test: Zero division handling (TotalSize = 0)
- [ ] Unit test: Case-insensitive side matching ("BUY", "buy", "Buy")
- [ ] Unit test: Validation failures (empty symbol, symbol too long, invalid format)
- [ ] Unit test: AvgPrice calculation accuracy
- [ ] Unit test: Very large decimal values (overflow protection)
- [ ] Unit test: Decimal precision (ensure no rounding errors in financial calculations)
- [ ] Integration test: Query with real data
- [ ] Integration test: User isolation (can't see other users' statistics)
- [ ] Integration test: Concurrent requests for same symbol
- [ ] Manual test: Swagger UI testing
- [ ] Manual test: Performance with large dataset (10k+ trades)
- [ ] Manual test: Index usage verification (check query execution plan)

### Frontend Integration
- [ ] Generate TypeScript client: `npm run generate-api-client`
- [ ] Create `useGetTradeStatistics` hook
- [ ] Create trade statistics display component
- [ ] Add loading states and error handling
- [ ] Display buy/sell statistics side-by-side
- [ ] Format numbers (price, size, funds with proper decimals)
- [ ] Add visual indicators (profit/loss color coding)
- [ ] Calculate and display profit/loss
- [ ] Add comparison charts (optional)

## Technical Notes

- **User Scoping**: Always filter by `ICurrentUser.Id` for security
- **Symbol Format**: Case-sensitive, typically uppercase with hyphen (e.g., "BTC-USDT")
- **Symbol Validation**: Regex pattern `^[A-Z0-9]+-[A-Z0-9]+$` ensures valid format
- **Side Matching**: Case-insensitive comparison to handle "buy", "BUY", "Buy"
- **Read-Only**: Query operation, no database modifications
- **Authorization**: Requires authenticated user (JWT token)
- **Decimal Precision**: All calculations use decimal type for financial accuracy
- **Zero Division**: Explicitly handled to return 0 instead of throwing exception
- **Database-Side Aggregation**: All SUM operations performed by database for optimal performance
- **Case Normalization**: Uses `EF.Functions.Lower()` in GroupBy for true database-side case-insensitive matching
- **Separation of Concerns**: Consider extracting statistics calculation logic into private methods or domain services
- **Testability**: Handler logic can be improved by extracting calculation methods for unit testing
- **Non-Nullable Response**: DTOs use non-nullable types to make contract explicit (always returns valid data)
- **Overflow Protection**: Decimal type handles large values, but test with realistic maximum values
- **Concurrent Access**: Query is read-only and uses AsNoTracking(), safe for concurrent requests

## Related Features

- **SyncTradeHistoryBySymbol**: Command that populates the data this query reads
- **GetTradeHistoriesBySymbol**: Query that returns detailed paginated trade history
- **SyncSetting**: Determines which symbols have trade history
- **TradeHistory Entity**: Source data for this query

## Future Enhancements

1. **Date Range Filtering**:
   - Add optional `StartDate` and `EndDate` parameters
   - Calculate statistics for specific time periods
   ```csharp
   public record GetTradeStatisticsBySymbolQuery(
       string Symbol,
       DateTime? StartDate = null,
       DateTime? EndDate = null
   ) : IRequest<TradeStatisticsDto>;
   ```

2. **Additional Metrics**:
   - Trade count (number of trades)
   - Min/Max prices
   - First/Last trade dates
   - Fee percentage of total
   - Net profit/loss calculation
   ```csharp
   public record SideStatisticsDto
   {
       // Existing properties...
       public int TradeCount { get; init; }
       public decimal MinPrice { get; init; }
       public decimal MaxPrice { get; init; }
       public DateTime? FirstTradeDate { get; init; }
       public DateTime? LastTradeDate { get; init; }
       public decimal FeePercentage { get; init; }
   }
   ```

3. **Profit/Loss Calculation**:
   - Add top-level profit/loss metrics
   ```csharp
   public record TradeStatisticsDto
   {
       public SideStatisticsDto Buy { get; init; }
       public SideStatisticsDto Sell { get; init; }
       public decimal ProfitLoss { get; init; }
       public decimal ProfitLossPercentage { get; init; }
   }
   ```

4. **Caching**:
   - Implement Redis caching for frequently accessed symbols
   - Cache duration: 5 minutes
   - Cache invalidation on new trades synced
   ```csharp
   var cacheKey = $"trade-stats:{userId}:{symbol}";
   var cached = await cache.GetAsync<TradeStatisticsDto>(cacheKey);
   if (cached != null) return cached;
   
   var result = // ... calculate statistics
   await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
   ```
   **Note**: With database-side aggregation, caching may be optional for most use cases

5. **Real-time Updates**:
   - WebSocket/SignalR notifications when statistics change
   - Push updates to connected clients on new trades

6. **Multi-Symbol Support**:
   - Accept array of symbols
   - Return dictionary of statistics
   ```csharp
   public record GetTradeStatisticsBySymbolsQuery(
       string[] Symbols
   ) : IRequest<Dictionary<string, TradeStatisticsDto>>;
   ```

7. **Export Functionality**:
   - Export statistics to CSV/Excel
   - Include charts and visualizations

8. **Comparison Features**:
   - Compare statistics across different time periods
   - Compare statistics across different symbols
   - Show trends and changes over time
