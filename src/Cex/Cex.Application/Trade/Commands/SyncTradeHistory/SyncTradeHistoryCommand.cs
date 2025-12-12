using Cex.Application.Common.Abstractions;
using Cex.Application.Common.Extensions;
using Cex.Domain.Enums;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using KuCoinTradeHistory = Lib.ExternalServices.KuCoin.Models.TradeHistory;
using TradeHistoryEntity = Cex.Domain.Entities.TradeHistory;
using ExchangeSettingEntity = Cex.Domain.Entities.ExchangeSetting;
using SyncSettingEntity = Cex.Domain.Entities.SyncSetting;

namespace Cex.Application.Trade.Commands.SyncTradeHistory
{
    public record SyncTradeHistoryCommand : IRequest;

    public class SyncTradeHistoryCommandHandler(
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        ILogTrace logTrace)
        : IRequestHandler<SyncTradeHistoryCommand>
    {
        private const int PageSize = 10;

        public async Task Handle(
            SyncTradeHistoryCommand request,
            CancellationToken cancellationToken)
        {
            var pageNumber = 1;

            logTrace.LogInformation("Starting SyncTradeHistory process");

            while (true)
            {
                var exchangeSettings = await cexDbContext.ExchangeSettings
                    .Where(x => x.ExchangeName == ExchangeName.KuCoin)
                    .Skip((pageNumber - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync(cancellationToken);

                if (exchangeSettings.Count == 0)
                {
                    logTrace.LogInformation("No more exchange settings to process");
                    break;
                }

                foreach (var exchangeSetting in exchangeSettings)
                {
                    try
                    {
                        await ProcessExchangeSetting(exchangeSetting, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg =
                            $"Error processing exchange setting for user {exchangeSetting.UserId}: {ex.Message}";
                        logTrace.LogError(errorMsg, ex);
                    }
                }

                pageNumber++;
            }

            logTrace.LogInformation("SyncTradeHistory completed");
        }

        private async Task ProcessExchangeSetting(
            ExchangeSettingEntity exchangeSetting,
            CancellationToken cancellationToken)
        {
            logTrace.LogInformation($"Processing exchange setting for user {exchangeSetting.UserId}");

            var syncSettings = await cexDbContext.SyncSettings
                .Where(x => x.UserId == exchangeSetting.UserId)
                .ToListAsync(cancellationToken);

            foreach (var syncSetting in syncSettings)
            {
                try
                {
                    await SyncTradeHistoryForSymbol(exchangeSetting, syncSetting, cancellationToken);
                }
                catch (Exception ex)
                {
                    var errorMsg =
                        $"Error syncing symbol {syncSetting.Symbol} for user {exchangeSetting.UserId}: {ex.Message}";
                    logTrace.LogError(errorMsg, ex);
                }
            }
        }

        private async Task SyncTradeHistoryForSymbol(
            ExchangeSettingEntity exchangeSetting,
            SyncSettingEntity syncSetting,
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

                    if (response?.Items != null && response.Items.Count != 0)
                    {
                        logTrace.LogInformation(
                            $"Fetched {response.Items.Count} trades for {syncSetting.Symbol} from {currentDate} to {nextDate}");

                        await DeleteAndInsertTradeHistory(
                            response.Items,
                            exchangeSetting.UserId,
                            syncSetting.Symbol,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg =
                        $"Error fetching trades for {syncSetting.Symbol} from {currentDate} to {nextDate}: {ex.Message}";
                    logTrace.LogError(errorMsg, ex);
                }

                currentDate = nextDate;
            }

            // Update LastSync
            syncSetting.LastSync = DateTime.UtcNow;
            cexDbContext.SyncSettings.Update(syncSetting);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            logTrace.LogInformation(
                $"Completed syncing {syncSetting.Symbol} for user {exchangeSetting.UserId}. Updated LastSync to {syncSetting.LastSync}");
        }

        private async Task DeleteAndInsertTradeHistory(
            List<KuCoinTradeHistory> trades,
            string userId,
            string symbol,
            CancellationToken cancellationToken)
        {
            if (trades.Count == 0)
            {
                return;
            }

            // Parse userId to long
            if (!long.TryParse(userId, out var userIdLong))
            {
                logTrace.LogWarning($"Invalid userId format: {userId}");
                return;
            }

            // Step 1: Extract all TradeIds from fetched trades
            var tradeIds = trades.Select(t => long.Parse(t.TradeId)).ToList();

            // Step 2: Delete existing trades with matching UserId and TradeIds
            var existingTrades = await cexDbContext.TradeHistories
                .Where(x => x.UserId == userIdLong && tradeIds.Contains(x.TradeId))
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
                UserId = userIdLong,
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

            var dbSet = cexDbContext.TradeHistories;
            foreach (var record in newRecords)
            {
                dbSet.Add(record);
            }

            await cexDbContext.SaveChangesAsync(cancellationToken);

            logTrace.LogInformation($"Inserted {newRecords.Count} new trades");
        }
    }
}