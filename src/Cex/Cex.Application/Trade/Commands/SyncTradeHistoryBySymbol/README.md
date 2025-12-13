# Sync Trade History By Symbol Feature

## Overview
Allows a user to manually trigger synchronization of trade history for a specific symbol from their configured exchange. Fetches trade history from the exchange API and stores records in the local database.

**Module Location**: `Cex.Application/Trade/Commands/SyncTradeHistoryBySymbol/`

**Version 1 Scope**: KuCoin exchange only

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
1. **User Scope**: Only sync trade history for the current authenticated user
2. **Symbol Filter**: Load sync setting based on current user and provided symbol
3. **Exchange Filter**: Load exchange setting for current user (KuCoin only)
4. **Date Range**: Start from LastSync, fetch in 7-day windows until current time
5. **Delete-Then-Insert Strategy**: Delete existing trades by UserId and TradeIds, then insert all fetched trades
6. **LastSync Update**: Update SyncSetting.LastSync to current UTC time after successful sync

## Backend Architecture

### Domain Layer
- `Cex.Domain/Entities/TradeHistory.cs` - Existing entity
- `Cex.Domain/Entities/SyncSetting.cs` - Used for tracking sync progress
- `Cex.Domain/Entities/ExchangeSetting.cs` - Exchange credentials

### Infrastructure Layer
- `Cex.Infrastructure/Data/Configurations/TradeHistoryConfiguration.cs` - Already exists
  - Composite key: `builder.HasKey(x => new { x.UserId, x.TradeId })`
  - Decimal precision: 13,6 for Price, Size, Funds, Fee, FeeRate
  - Default value: CreatedAt = DateTime.UtcNow

### Application Layer
Location: `Cex.Application/Trade/Commands/SyncTradeHistoryBySymbol/`

**Commands**:
- `Commands/SyncTradeHistoryBySymbol/SyncTradeHistoryBySymbolCommand.cs`
  - Input: Symbol (string)
  - Returns: Sync result with statistics
  ```csharp
  public record SyncTradeHistoryBySymbolCommand(string Symbol) : IRequest<SyncTradeHistoryBySymbolResult>;
  
  public class SyncTradeHistoryBySymbolResult
  {
      public int TotalSynced { get; set; }
      public decimal TotalBuy { get; set; }
      public decimal TotalSell { get; set; }
      public decimal Profit { get; set; }
  }
  ```

**Dependencies**:
- `IKuCoinService` - External service for fetching trade history
- `ICexDbContext` - Database context
- `ICurrentUser` - User context (required to get current user ID)
- `ILogTrace` - Logging service

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

#### Step 1: Load Settings in Parallel
```csharp
public async Task<SyncTradeHistoryBySymbolResult> Handle(
    SyncTradeHistoryBySymbolCommand request,
    CancellationToken cancellationToken)
{
    var userId = currentUser.Id;
    var result = new SyncTradeHistoryBySymbolResult();
    
    // Load both settings in parallel
    var syncSettingTask = cexDbContext.SyncSettings
        .FirstOrDefaultAsync(x => x.UserId == userId && x.Symbol == request.Symbol, cancellationToken);
    
    var exchangeSettingTask = cexDbContext.ExchangeSettings
        .FirstOrDefaultAsync(x => x.UserId == userId && x.ExchangeName == ExchangeName.KuCoin, cancellationToken);
    
    await Task.WhenAll(syncSettingTask, exchangeSettingTask);
    
    var syncSetting = await syncSettingTask;
    var exchangeSetting = await exchangeSettingTask;
    
    if (syncSetting == null || exchangeSetting == null)
    {
        logTrace.LogWarning($"Settings not found for user {userId} and symbol {request.Symbol}");
        return result;
    }
    
    await SyncTradeHistoryForSymbol(exchangeSetting, syncSetting, result, cancellationToken);
    
    return result;
}
```

#### Step 2: Sync Trade History (7-Day Batches)
```csharp
private async Task SyncTradeHistoryForSymbol(
    ExchangeSetting exchangeSetting,
    SyncSetting syncSetting,
    SyncTradeHistoryBySymbolResult result,
    CancellationToken cancellationToken)
{
    var currentDate = syncSetting.LastSync;
    var endDate = DateTime.UtcNow;
    
    while (currentDate < endDate)
    {
        var nextDate = currentDate.AddDays(7);
        if (nextDate > endDate) nextDate = endDate;
        
        // Fetch trades from KuCoin
        var response = await kuCoinService.GetTradeHistory(
            symbol: syncSetting.Symbol,
            fromDate: currentDate,
            credentials: exchangeSetting.ToKuCoinConfig());
        
        if (response?.Items != null && response.Items.Count > 0)
        {
            await DeleteAndInsertTradeHistory(
                response.Items,
                exchangeSetting.UserId,
                syncSetting.Symbol,
                result,
                cancellationToken);
        }
        
        currentDate = nextDate;
    }
    
    // Update LastSync
    syncSetting.LastSync = DateTime.UtcNow;
    cexDbContext.SyncSettings.Update(syncSetting);
    await cexDbContext.SaveChangesAsync(cancellationToken);
}
```

#### Step 3: Delete and Insert Trade History
```csharp
private async Task DeleteAndInsertTradeHistory(
    List<KuCoinTradeHistory> trades,
    string userId,
    string symbol,
    SyncTradeHistoryBySymbolResult result,
    CancellationToken cancellationToken)
{
    if (trades.Count == 0) return;
    
    // Step 1: Extract all TradeIds from fetched trades
    var tradeIds = trades.Select(t => long.Parse(t.TradeId)).ToList();
    
    // Step 2: Delete existing trades with matching UserId and TradeIds
    var existingTrades = await cexDbContext.TradeHistories
        .Where(x => x.UserId == userId && tradeIds.Contains(x.TradeId))
        .ToListAsync(cancellationToken);
    
    if (existingTrades.Count > 0)
    {
        cexDbContext.TradeHistories.RemoveRange(existingTrades);
        await cexDbContext.SaveChangesAsync(cancellationToken);
    }
    
    // Step 3: Insert all fetched trades
    var newRecords = trades.Select(trade => new TradeHistory
    {
        UserId = userId,
        Symbol = symbol,
        TradeId = long.Parse(trade.TradeId),
        OrderId = trade.OrderId,
        CounterOrderId = trade.CounterOrderId,
        Side = trade.Side,
        Liquidity = trade.Liquidity,
        ForceTaker = trade.ForceTaker,
        Price = decimal.Parse(trade.Price),
        Size = decimal.Parse(trade.Size),
        Funds = decimal.Parse(trade.Funds),
        Fee = decimal.Parse(trade.Fee),
        FeeRate = decimal.Parse(trade.FeeRate),
        FeeCurrency = trade.FeeCurrency,
        Stop = trade.Stop,
        TradeType = trade.TradeType,
        Type = trade.Type,
        TradedAt = DateTimeOffset.FromUnixTimeMilliseconds(trade.CreatedAt).UtcDateTime,
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(trade.CreatedAt).UtcDateTime
    }).ToList();
    
    cexDbContext.TradeHistories.AddRange(newRecords);
    await cexDbContext.SaveChangesAsync(cancellationToken);
    
    // Step 4: Calculate statistics
    result.TotalSynced += newRecords.Count;
    
    foreach (var record in newRecords)
    {
        var funds = record.Funds + record.Fee; // Total cost including fee
        
        if (record.Side.Equals("buy", StringComparison.OrdinalIgnoreCase))
        {
            result.TotalBuy += funds;
        }
        else if (record.Side.Equals("sell", StringComparison.OrdinalIgnoreCase))
        {
            result.TotalSell += funds;
        }
    }
    
    // Profit = Total Sell - Total Buy
    result.Profit = result.TotalSell - result.TotalBuy;
}
```

**Performance Benefits**:
- **Parallel Loading**: Loads SyncSetting and ExchangeSetting concurrently
- **Simpler Logic**: No need to check each trade individually
- **Bulk Operations**: Uses `RemoveRange` and `AddRange` for better performance
- **Single Query**: One delete query for all matching TradeIds
- **Faster Execution**: Avoids N+1 query problem

### API Layer
**Controller**: `WebAPI/Controllers/TradeController.cs`

**Endpoints**:
- `POST /api/trade/sync-by-symbol` → Triggers sync for specific symbol, returns sync statistics
  - Request body: `{ "symbol": "BTC-USDT" }`
  - Response: `200 OK` with `SyncTradeHistoryBySymbolResult`
    ```json
    {
      "totalSynced": 150,
      "totalBuy": 45000.50,
      "totalSell": 48500.75,
      "profit": 3500.25
    }
    ```
  - Requires authentication
  - User-scoped (automatically uses current user)

## Performance Considerations

1. **Parallel Loading**: Load settings concurrently using `Task.WhenAll`
2. **Bulk Operations**: Use `RemoveRange` and `AddRange` instead of individual operations
3. **Transaction Scope**: SaveChanges after delete, then after insert (2 operations per 7-day batch)
4. **API Rate Limits**: Implement delays between KuCoin API calls if needed
5. **Logging**: Log progress for sync operation
6. **Error Isolation**: Log errors but don't expose sensitive details to user

## Error Handling

- **Settings Not Found**: Log warning, return early
- **Exchange API Errors**: Log error, throw exception to notify user
- **Database Errors**: Log error, throw exception
- **Validation Errors**: Skip invalid trades, log warning
- **Rate Limiting**: Implement retry with exponential backoff (future enhancement)

## Frontend Integration

### Sync Setting List View (`/sync-setting`)
**Location**: `src/WebUI.React/src/features/settings/sync-setting/list/`

**UI Changes**:
1. **Add Sync Icon Button**: Add sync icon button in the actions column
2. **Click Handler**: On click, send POST request to `/api/trade/sync-by-symbol`
3. **Loading State**: Show loading indicator while syncing
4. **Success Feedback**: Show success toast notification
5. **Error Feedback**: Show error toast notification

**Example Implementation**:
```typescript
import { useMutation } from '@tanstack/react-query';
import { tradeClient } from 'store/api-client';
import SyncIcon from '@mui/icons-material/Sync';
import IconButton from '@mui/material/IconButton';

interface SyncResult {
  totalSynced: number;
  totalBuy: number;
  totalSell: number;
  profit: number;
}

function useSyncTradeHistoryBySymbol() {
  return useMutation({
    mutationFn: (symbol: string) => 
      tradeClient.syncBySymbol({ symbol }),
    onSuccess: (result: SyncResult) => {
      toast.success(
        `Successfully synced ${result.totalSynced} trades. ` +
        `Buy: $${result.totalBuy.toFixed(2)}, ` +
        `Sell: $${result.totalSell.toFixed(2)}, ` +
        `Profit: $${result.profit.toFixed(2)}`
      );
    },
    onError: (error) => {
      toast.error('Failed to sync trade history');
    },
  });
}

// In the table actions column
<IconButton
  onClick={() => syncMutation.mutate(row.symbol)}
  disabled={syncMutation.isLoading}
>
  <SyncIcon />
</IconButton>
```

## Implementation Checklist

### Backend
- [ ] Create `SyncTradeHistoryBySymbolCommand` and handler
- [ ] Implement parallel settings loading with `Task.WhenAll`
- [ ] Implement 7-day batch loop for KuCoin API
- [ ] Implement delete-then-insert logic (bulk operations)
- [ ] Implement LastSync update
- [ ] Add endpoint to `TradeController`
- [ ] Add authorization and user scoping
- [ ] Write unit tests
- [ ] Write integration tests
- [ ] Add logging with `ILogTrace`

### Frontend
- [ ] Create `useSyncTradeHistoryBySymbol` hook
- [ ] Add sync icon button to sync setting list actions column
- [ ] Implement loading state
- [ ] Add success/error toast notifications
- [ ] Handle edge cases (no settings found, API errors)
- [ ] Add confirmation dialog (optional)

### Testing
- [ ] Unit test: parallel settings loading
- [ ] Unit test: date range calculation (7-day batches)
- [ ] Unit test: delete-then-insert logic
- [ ] Integration test: successful sync
- [ ] Integration test: settings not found
- [ ] Integration test: duplicate trades handling
- [ ] Manual test: UI sync button
- [ ] Manual test: toast notifications

## Technical Notes

- **User Scoping**: Uses `ICurrentUser.Id` to ensure users can only sync their own data
- **Exchange Filter (v1)**: Only supports ExchangeName = KuCoin
- **Date Range**: LastSync to current UTC time, processed in 7-day windows
- **Idempotency**: Safe to run multiple times (delete-then-insert ensures fresh data)
- **Delete Strategy**: Only deletes trades matching fetched TradeIds (preserves other trades)
- **Authorization**: Requires authenticated user
- **Future Enhancements**: Support for Binance, Coinbase; real-time progress updates; batch sync multiple symbols

## Related Features

- **SyncSetting**: Provides symbol and date range configuration
- **ExchangeSetting**: Provides exchange credentials (ApiKey, Secret, Passphrase)
- **KuCoinService**: External service for API integration
- **SyncTradeHistory**: Batch sync all symbols (related command)
