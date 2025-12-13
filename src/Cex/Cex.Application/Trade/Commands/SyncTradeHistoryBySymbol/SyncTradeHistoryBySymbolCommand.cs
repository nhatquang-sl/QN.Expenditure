using Cex.Application.Common.Abstractions;
using Cex.Application.Common.Extensions;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using KuCoinTradeHistory = Lib.ExternalServices.KuCoin.Models.TradeHistory;
using TradeHistoryEntity = Cex.Domain.Entities.TradeHistory;
using ExchangeSettingEntity = Cex.Domain.Entities.ExchangeSetting;
using SyncSettingEntity = Cex.Domain.Entities.SyncSetting;
using Lib.Application.Extensions;

namespace Cex.Application.Trade.Commands.SyncTradeHistoryBySymbol;

public record SyncTradeHistoryBySymbolCommand(string Symbol) : IRequest<SyncTradeHistoryBySymbolResult>;

public class SyncTradeHistoryBySymbolResult
{
    public int TotalSynced { get; set; }
    public decimal TotalBuy { get; set; }
    public decimal TotalSell { get; set; }
    public decimal TotalBuySize { get; set; }
    public decimal TotalSellSize { get; set; }
    public decimal Profit { get; set; }
    public decimal AvgBuyPrice { get; set; }
    public decimal AvgSellPrice { get; set; }
}

public class SyncTradeHistoryBySymbolCommandHandler(
    ICexDbContext cexDbContext,
    IKuCoinService kuCoinService,
    ICurrentUser currentUser,
    ILogTrace logTrace)
    : IRequestHandler<SyncTradeHistoryBySymbolCommand, SyncTradeHistoryBySymbolResult>
{
    public async Task<SyncTradeHistoryBySymbolResult> Handle(
        SyncTradeHistoryBySymbolCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var result = new SyncTradeHistoryBySymbolResult();

        logTrace.LogInformation($"Starting sync for symbol {request.Symbol} and user {userId}");

        // Load settings sequentially to avoid DbContext concurrency issues
        var syncSetting = await cexDbContext.SyncSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Symbol == request.Symbol, cancellationToken);

        var exchangeSetting = await cexDbContext.ExchangeSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ExchangeName == ExchangeName.KuCoin, cancellationToken);

        if (syncSetting == null || exchangeSetting == null)
        {
            logTrace.LogWarning($"Settings not found for user {userId} and symbol {request.Symbol}");
            logTrace.Flush();
            return result;
        }

        await SyncTradeHistoryForSymbol(exchangeSetting, syncSetting, result, cancellationToken);

        logTrace.LogInformation(
            $"Completed sync for symbol {request.Symbol}. Synced: {result.TotalSynced}, " +
            $"Buy: {result.TotalBuy}, Sell: {result.TotalSell}, Profit: {result.Profit}");
        logTrace.Flush();

        return result;
    }

    private async Task SyncTradeHistoryForSymbol(
        ExchangeSettingEntity exchangeSetting,
        SyncSettingEntity syncSetting,
        SyncTradeHistoryBySymbolResult result,
        CancellationToken cancellationToken)
    {
        logTrace.LogInformation(
            $"Syncing symbol {syncSetting.Symbol} for user {exchangeSetting.UserId} from {syncSetting.LastSync}");

        var currentDate = syncSetting.LastSync;
        var endDate = DateTime.UtcNow;

        while (currentDate < endDate)
        {
            var nextDate = currentDate.AddDays(7);
            if (nextDate > endDate)
            {
                nextDate = endDate;
            }

            try
            {
                // Fetch trades from KuCoin
                var response = await kuCoinService.GetTradeHistory(
                    syncSetting.Symbol,
                    currentDate,
                    exchangeSetting.ToKuCoinConfig());

                if (response?.Items != null && response.Items.Count > 0)
                {
                    logTrace.LogInformation(
                        $"Fetched {response.Items.Count} trades for {syncSetting.Symbol} from {currentDate} to {nextDate}");

                    await DeleteAndInsertTradeHistory(
                        response.Items,
                        exchangeSetting.UserId,
                        syncSetting.Symbol,
                        result,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error fetching trades for {syncSetting.Symbol} from {currentDate} to {nextDate}: {ex.Message}";
                logTrace.LogError(errorMsg, ex);
                throw;
            }

            currentDate = nextDate;
        }

        // Update LastSync
        syncSetting.LastSync = DateTime.UtcNow;
        cexDbContext.SyncSettings.Update(syncSetting);
        await cexDbContext.SaveChangesAsync(cancellationToken);

        // Calculate average prices (total funds / total size)
        if (result.TotalBuySize > 0)
        {
            result.AvgBuyPrice = (result.TotalBuy / result.TotalBuySize).FixedNumber();
        }

        if (result.TotalSellSize > 0)
        {
            result.AvgSellPrice = (result.TotalSell / result.TotalSellSize).FixedNumber();
        }

        // Profit = Total Sell - Total Buy
        result.Profit = result.TotalSell - result.TotalBuy;

        logTrace.LogInformation(
            $"Completed syncing {syncSetting.Symbol} for user {exchangeSetting.UserId}. Updated LastSync to {syncSetting.LastSync}");
    }

    private async Task DeleteAndInsertTradeHistory(
        List<KuCoinTradeHistory> trades,
        string userId,
        string symbol,
        SyncTradeHistoryBySymbolResult result,
        CancellationToken cancellationToken)
    {
        if (trades.Count == 0)
        {
            return;
        }

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
            logTrace.LogInformation($"Deleted {existingTrades.Count} existing trades");
        }

        // Step 3: Insert all fetched trades
        var newRecords = trades.Select(trade => new TradeHistoryEntity
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

        logTrace.LogInformation($"Inserted {newRecords.Count} new trades");

        // Step 4: Accumulate statistics
        result.TotalSynced += newRecords.Count;

        foreach (var record in newRecords)
        {
            var funds = record.Funds + record.Fee; // Total cost including fee

            if (record.Side.Equals("buy", StringComparison.OrdinalIgnoreCase))
            {
                result.TotalBuy += funds;
                result.TotalBuySize += record.Size;
            }
            else if (record.Side.Equals("sell", StringComparison.OrdinalIgnoreCase))
            {
                result.TotalSell += funds;
                result.TotalSellSize += record.Size;
            }
        }
    }
}
