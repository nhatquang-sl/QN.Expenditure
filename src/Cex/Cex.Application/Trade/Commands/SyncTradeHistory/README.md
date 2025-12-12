# Sync Trade History Feature

## Overview
Synchronizes cryptocurrency trade history from exchanges to the local database. Processes all configured exchange settings and sync settings in batches, fetches trade history from exchange APIs, and stores records in the database.

**Module Location**: `Cex.Application/Trade/Commands/SyncTradeHistory/`

**Version 1 Scope**: KuCoin exchange only

## Data Model

### Entity: TradeHistory (Existing)
```csharp
public class TradeHistory
{
    public long UserId { get; set; }
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
1. **Exchange Settings Pagination**: Process 10 exchange settings per batch
2. **Sync Settings Filter**: Load sync settings for each exchange setting's user
3. **Date Range**: Start from LastSync (not StartSync), fetch in 7-day windows until current time
4. **Delete-Then-Insert Strategy**: Delete existing trades by UserId and TradeIds, then insert all fetched trades (faster/easier than upsert)
5. **LastSync Update**: Update SyncSetting.LastSync to current UTC time after successful sync

## Backend Architecture

### Domain Layer
- `Cex.Domain/Entities/TradeHistory.cs` - Existing entity (already defined)
- `Cex.Domain/Entities/SyncSetting.cs` - Used for tracking sync progress
- `Cex.Domain/Entities/ExchangeSetting.cs` - Exchange credentials

### Infrastructure Layer
- `Cex.Infrastructure/Data/Configurations/TradeHistoryConfiguration.cs` - Already exists
  - Composite key: `builder.HasKey(x => new { x.UserId, x.TradeId })`
  - Decimal precision: 13,6 for Price, Size, Funds, Fee, FeeRate
  - Default value: CreatedAt = DateTime.UtcNow

### Application Layer
Location: `Cex.Application/Trade/Commands/SyncTradeHistory/`

**Commands**:
- `Commands/SyncTradeHistory/SyncTradeHistoryCommand.cs`
  - No input parameters (processes all settings)
  - Returns summary: total processed, inserted, updated, errors
  ```csharp
  public record SyncTradeHistoryCommand() : IRequest<SyncTradeHistoryResult>;
  
  public class SyncTradeHistoryResult
  {
      public int TotalExchangeSettings { get; set; }
      public int TotalSyncSettings { get; set; }
      public int TotalTradesProcessed { get; set; }
      public int TotalTradesInserted { get; set; }
      public int TotalTradesUpdated { get; set; }
      public List<string> Errors { get; set; } = new();
  }
  ```

**Dependencies**:
- `IKuCoinService` - External service for fetching trade history
- `ICexDbContext` - Database context
- `ICurrentUser` - User context (may not be needed for background jobs)

**Extensions**:
- `ExchangeSettingExtensions.ToKuCoinConfig()` - Converts `ExchangeSetting` to `KuCoinConfig`
  ```csharp
  // Location: Cex.Application/Common/Extensions/ExchangeSettingExtensions.cs
  public static KuCoinConfig ToKuCoinConfig(this ExchangeSetting exchangeSetting)
  {
      return new KuCoinConfig
      {
          ApiKey = exchangeSetting.ApiKey,
          ApiSecret = exchangeSetting.Secret,
          ApiPassphrase = exchangeSetting.Passphrase ?? string.Empty
      };
  }
  ```

### Algorithm

#### Step 1: Paginate Exchange Settings
```csharp
var pageNumber = 1;
const int pageSize = 10;

while (true)
{
    var exchangeSettings = await cexDbContext.ExchangeSettings
        .Where(x => x.ExchangeName == ExchangeName.KuCoin)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);
    
    if (!exchangeSettings.Any()) break;
    
    foreach (var exchangeSetting in exchangeSettings)
    {
        await ProcessExchangeSetting(exchangeSetting, cancellationToken);
    }
    
    pageNumber++;
}
```

#### Step 2: Load Sync Settings
```csharp
private async Task ProcessExchangeSetting(
    ExchangeSetting exchangeSetting, 
    CancellationToken cancellationToken)
{
    var syncSettings = await cexDbContext.SyncSettings
        .Where(x => x.UserId == exchangeSetting.UserId)
        .ToListAsync(cancellationToken);
    
    foreach (var syncSetting in syncSettings)
    {
        await SyncTradeHistoryForSymbol(exchangeSetting, syncSetting, cancellationToken);
    }
}
```

#### Step 3: Sync Trade History (7-Day Batches)
```csharp
private async Task SyncTradeHistoryForSymbol(
    ExchangeSetting exchangeSetting,
    SyncSetting syncSetting,
    CancellationToken cancellationToken)
{
    var currentDate = syncSetting.LastSync;  // Start from LastSync, not StartSync
    var endDate = DateTime.UtcNow;
    
    while (currentDate < endDate)
    {
        var nextDate = currentDate.AddDays(7);
        if (nextDate > endDate) nextDate = endDate;
        
        // Fetch trades from KuCoin
        var trades = await kuCoinService.GetTradeHistory(
            symbol: syncSetting.Symbol,
            fromDate: currentDate,
            credentials: exchangeSetting.ToKuCoinConfig()
        );
        
        // Delete existing trades, then insert
        await DeleteAndInsertTradeHistory(trades, exchangeSetting.UserId, syncSetting.Symbol, cancellationToken);
        
        currentDate = nextDate;
    }
    
    // Update LastSync
    syncSetting.LastSync = DateTime.UtcNow;
    cexDbContext.SyncSettings.Update(syncSetting);
    await cexDbContext.SaveChangesAsync(cancellationToken);
}
```

#### Step 4: Delete and Insert Trade History
```csharp
private async Task DeleteAndInsertTradeHistory(
    IEnumerable<KuCoinTrade> trades,
    long userId,
    string symbol,
    CancellationToken cancellationToken)
{
    if (!trades.Any()) return;
    
    // Step 1: Extract all TradeIds from fetched trades
    var tradeIds = trades.Select(t => t.TradeId).ToList();
    
    // Step 2: Delete existing trades with matching UserId and TradeIds
    var existingTrades = await cexDbContext.TradeHistories
        .Where(x => x.UserId == userId && tradeIds.Contains(x.TradeId))
        .ToListAsync(cancellationToken);
    
    if (existingTrades.Any())
    {
        cexDbContext.TradeHistories.RemoveRange(existingTrades);
        await cexDbContext.SaveChangesAsync(cancellationToken);
    }
    
    // Step 3: Insert all fetched trades
    var newRecords = trades.Select(trade => new TradeHistory
    {
        UserId = userId,
        Symbol = symbol,
        TradeId = trade.TradeId,
        OrderId = trade.OrderId,
        CounterOrderId = trade.CounterOrderId,
        Side = trade.Side,
        Liquidity = trade.Liquidity,
        ForceTaker = trade.ForceTaker,
        Price = trade.Price,
        Size = trade.Size,
        Funds = trade.Funds,
        Fee = trade.Fee,
        FeeRate = trade.FeeRate,
        FeeCurrency = trade.FeeCurrency,
        Stop = trade.Stop,
        TradeType = trade.TradeType,
        Type = trade.Type,
        TradedAt = trade.TradedAt,
        CreatedAt = trade.CreatedAt
    }).ToList();
    
    cexDbContext.TradeHistories.AddRange(newRecords);
    await cexDbContext.SaveChangesAsync(cancellationToken);
}
```

**Performance Benefits**:
- **Simpler Logic**: No need to check each trade individually
- **Bulk Operations**: Uses `RemoveRange` and `AddRange` for better performance
- **Single Query**: One delete query for all matching TradeIds
- **Faster Execution**: Avoids N+1 query problem from checking existence per trade

### API Layer
**Controller**: `WebAPI/Controllers/SyncTradeHistoryController.cs`

**Endpoints**:
- `POST /api/sync/trade-history` → Triggers sync, returns `SyncTradeHistoryResult`
  - No request body required
  - May be invoked by background job or manual trigger
  - Returns summary of sync operation

## Performance Considerations

1. **Pagination**: Process 10 exchange settings per batch to avoid memory issues
2. **Bulk Operations**: Use `RemoveRange` and `AddRange` instead of individual operations
3. **Transaction Scope**: SaveChanges after delete, then after insert (2 operations per 7-day batch)
4. **API Rate Limits**: Implement delays between KuCoin API calls if needed
5. **Logging**: Log progress for each exchange setting and sync setting
6. **Error Isolation**: Continue processing if one exchange/symbol fails

## Error Handling

- **Exchange API Errors**: Log error, skip to next sync setting, include in result.Errors
- **Database Errors**: Log error, rollback batch, continue to next batch
- **Validation Errors**: Skip invalid trades, log warning
- **Rate Limiting**: Implement retry with exponential backoff (future enhancement)

## Implementation Checklist

### Backend
- [x] TradeHistory entity already exists in Domain
- [x] TradeHistoryConfiguration already exists in Infrastructure
- [ ] Add `DbSet<TradeHistory>` to ICexDbContext and CexDbContext (if not exists)
- [ ] Create migration if needed: `dotnet ef migrations add SyncTradeHistory`
- [ ] Implement `SyncTradeHistoryCommand` and handler
- [ ] Implement pagination logic (10 per page)
- [ ] Implement 7-day batch loop for KuCoin API
- [ ] Implement delete-then-insert logic (bulk operations)
- [ ] Implement LastSync update
- [ ] Create `SyncTradeHistoryController`
- [ ] Write integration tests
- [ ] Add logging for progress tracking

### Testing
- [ ] Unit tests for pagination logic
- [ ] Unit tests for date range calculation (7-day batches)
- [ ] Unit tests for delete-then-insert logic
- [ ] Integration test: small date range (1 month)
- [ ] Integration test: large date range (1 year+)
- [ ] Integration test: duplicate trades handling (delete existing, insert new)
- [ ] Manual test: no trades scenario
- [ ] Manual test: API error handling

## Technical Notes

- **Exchange Filter (v1)**: Only process ExchangeName = KuCoin
- **Sync Settings**: Uses existing SyncSetting entities to determine what to sync
- **Date Range**: LastSync to current UTC time, processed in 7-day windows (StartSync only used on first sync when LastSync = StartSync)
- **Idempotency**: Safe to run multiple times (delete-then-insert ensures fresh data)
- **Delete Strategy**: Only deletes trades matching fetched TradeIds (preserves other trades)
- **Background Job**: Can be triggered by scheduled task or manual API call
- **Future Enhancements**: Support for Binance, Coinbase; parallel processing; progress tracking

## Related Features

- **SyncSetting**: Provides symbol and date range configuration
- **ExchangeSetting**: Provides exchange credentials (ApiKey, Secret, Passphrase)
- **KuCoinService**: External service for API integration