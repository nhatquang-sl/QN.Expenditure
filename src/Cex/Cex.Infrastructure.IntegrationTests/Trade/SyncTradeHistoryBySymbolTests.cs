using Cex.Application.Common.Abstractions;
using Cex.Application.Trade.Commands.SyncTradeHistoryBySymbol;
using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Trade;

public class SyncTradeHistoryBySymbolTests : DependencyInjectionFixture
{
    private readonly ICexDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;
    private readonly Mock<IKuCoinService> _kuCoinServiceMock;

    public SyncTradeHistoryBySymbolTests()
    {
        _sender = GetService<ISender>();
        _context = GetService<ICexDbContext>();
        _currentUser = GetService<ICurrentUser>();
        _kuCoinServiceMock = KuCoinServiceMock;
    }

    [Fact]
    public async Task Success_SmallDateRange_OneMonth()
    {
        // Arrange
        var symbol = "BTCUSDT";
        var startDate = DateTime.UtcNow.AddMonths(-1);
        await SetupExchangeAndSyncSettings(symbol, startDate);

        var mockTrades = CreateMockTrades(symbol, startDate, 10);
        SetupKuCoinServiceMock(mockTrades);

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(10);
        result.TotalBuy.ShouldBeGreaterThan(0);
        result.TotalSell.ShouldBeGreaterThan(0);
        result.Profit.ShouldNotBe(0);

        var savedTrades = await _context.TradeHistories
            .Where(x => x.UserId == _currentUser.Id && x.Symbol == symbol)
            .ToListAsync();

        savedTrades.Count.ShouldBe(10);
    }

    [Fact]
    public async Task Success_LargeDateRange_OneYear()
    {
        // Arrange
        var symbol = "ETHUSDT";
        var startDate = DateTime.UtcNow.AddYears(-1);
        await SetupExchangeAndSyncSettings(symbol, startDate);

        // Mock trades for multiple 7-day batches
        var mockTrades = CreateMockTrades(symbol, startDate, 100);
        SetupKuCoinServiceMock(mockTrades);

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(100);

        var savedTrades = await _context.TradeHistories
            .Where(x => x.UserId == _currentUser.Id && x.Symbol == symbol)
            .ToListAsync();

        savedTrades.Count.ShouldBe(100);

        // Verify LastSync was updated
        var syncSetting = await _context.SyncSettings
            .FirstAsync(x => x.UserId == _currentUser.Id && x.Symbol == symbol);
        syncSetting.LastSync.ShouldBeGreaterThan(startDate);
    }

    [Fact]
    public async Task Success_DuplicateTrades_DeleteThenInsert()
    {
        // Arrange
        var symbol = "BNBUSDT";
        var startDate = DateTime.UtcNow.AddDays(-7);
        await SetupExchangeAndSyncSettings(symbol, startDate);

        // Create initial trades with specific IDs
        var initialTradeIds = new List<long> { 1001, 1002, 1003, 1004, 1005 };
        var initialTrades = initialTradeIds.Select((id, index) =>
        {
            var trade = CreateMockTrade(symbol, "buy", 100m, 1m, 0.1m, startDate.AddHours(index * 6));
            // Use reflection or recreate with specific TradeId - since we can't modify, generate with fixed seed
            return CreateMockTradeWithId(symbol, "buy", 100m, 1m, 0.1m, id, startDate.AddHours(index * 6));
        }).ToList();

        SetupKuCoinServiceMock(initialTrades);
        await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        var savedTradesBeforeSync = await _context.TradeHistories
            .Where(x => x.UserId == _currentUser.Id && x.Symbol == symbol)
            .ToListAsync();
        savedTradesBeforeSync.Count.ShouldBe(5);

        // Mock overlapping trades (3 duplicates: 1003, 1004, 1005 + 3 new: 1006, 1007, 1008)
        var overlappingTradeIds = new List<long> { 1003, 1004, 1005, 1006, 1007, 1008 };
        var overlappingTrades = overlappingTradeIds.Select((id, index) =>
        {
            return CreateMockTradeWithId(symbol, index % 2 == 0 ? "buy" : "sell", 100m, 1m, 0.1m, id, startDate.AddHours(index * 6));
        }).ToList();

        SetupKuCoinServiceMock(overlappingTrades);

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(6); // Only the 6 trades from the second sync

        var savedTrades = await _context.TradeHistories
            .Where(x => x.UserId == _currentUser.Id && x.Symbol == symbol)
            .ToListAsync();

        // Should have: 1001, 1002 (not re-synced) + 1003, 1004, 1005, 1006, 1007, 1008 (from second sync) = 8 total
        savedTrades.Count.ShouldBe(8);
    }

    [Fact]
    public async Task Success_NoTrades_ReturnsEmptyResult()
    {
        // Arrange
        var symbol = "DOGEUSDT";
        var startDate = DateTime.UtcNow.AddDays(-1);
        await SetupExchangeAndSyncSettings(symbol, startDate);

        // Mock empty trade list
        SetupKuCoinServiceMock(new List<Lib.ExternalServices.KuCoin.Models.TradeHistory>());

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(0);
        result.TotalBuy.ShouldBe(0);
        result.TotalSell.ShouldBe(0);
        result.Profit.ShouldBe(0);

        var savedTrades = await _context.TradeHistories
            .Where(x => x.UserId == _currentUser.Id && x.Symbol == symbol)
            .ToListAsync();

        savedTrades.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Failure_NoSyncSetting_ReturnsEmptyResult()
    {
        // Arrange
        var symbol = "XRPUSDT";

        // Only create exchange setting, no sync setting
        var exchangeSetting = new ExchangeSetting
        {
            UserId = _currentUser.Id,
            ExchangeName = ExchangeName.KuCoin,
            ApiKey = "test-api-key",
            Secret = "test-secret",
            Passphrase = "test-passphrase"
        };
        _context.ExchangeSettings.Add(exchangeSetting);
        await _context.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(0);
        result.TotalBuy.ShouldBe(0);
        result.TotalSell.ShouldBe(0);
        result.Profit.ShouldBe(0);
    }

    [Fact]
    public async Task Failure_NoExchangeSetting_ReturnsEmptyResult()
    {
        // Arrange
        var symbol = "ADAUSDT";
        var startDate = DateTime.UtcNow.AddDays(-7);

        // Only create sync setting, no exchange setting
        var syncSetting = new SyncSetting
        {
            UserId = _currentUser.Id,
            Symbol = symbol,
            StartSync = startDate,
            LastSync = DateTime.UtcNow
        };
        _context.SyncSettings.Add(syncSetting);
        await _context.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(0);
        result.TotalBuy.ShouldBe(0);
        result.TotalSell.ShouldBe(0);
        result.Profit.ShouldBe(0);
    }

    [Fact]
    public async Task Success_CalculatesStatistics_Correctly()
    {
        // Arrange
        var symbol = "SOLUSDT";
        var startDate = DateTime.UtcNow.AddDays(-7);
        await SetupExchangeAndSyncSettings(symbol, startDate);

        // Create trades with known values
        var mockTrades = new List<Lib.ExternalServices.KuCoin.Models.TradeHistory>
        {
            CreateMockTrade(symbol, "buy", 100m, 1m, 0.1m),
            CreateMockTrade(symbol, "buy", 105m, 2m, 0.2m),
            CreateMockTrade(symbol, "sell", 110m, 1.5m, 0.15m),
        };
        SetupKuCoinServiceMock(mockTrades);

        // Act
        var result = await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        result.ShouldNotBeNull();
        result.TotalSynced.ShouldBe(3);

        // TotalBuy = (funds + fee) for each buy trade
        // Buy 1: 100 + 0.1 = 100.1
        // Buy 2: 210 + 0.2 = 210.2
        // Total Buy = 310.3
        result.TotalBuy.ShouldBe(310.3m);

        // TotalSell = (funds + fee) for sell trade
        // Sell 1: 165 + 0.15 = 165.15
        result.TotalSell.ShouldBe(165.15m);

        // Profit = TotalSell - TotalBuy = 165.15 - 310.3 = -145.15
        result.Profit.ShouldBe(-145.15m);
    }

    [Fact]
    public async Task Success_UpdatesLastSync_AfterSuccessfulSync()
    {
        // Arrange
        var symbol = "MATICUSDT";
        var startDate = DateTime.UtcNow.AddDays(-30);
        await SetupExchangeAndSyncSettings(symbol, startDate);

        var syncSettingBefore = await _context.SyncSettings
            .FirstAsync(x => x.UserId == _currentUser.Id && x.Symbol == symbol);
        var lastSyncBefore = syncSettingBefore.LastSync;

        var mockTrades = CreateMockTrades(symbol, startDate, 5);
        SetupKuCoinServiceMock(mockTrades);

        // Act
        await _sender.Send(new SyncTradeHistoryBySymbolCommand(symbol));

        // Assert
        var syncSettingAfter = await _context.SyncSettings
            .FirstAsync(x => x.UserId == _currentUser.Id && x.Symbol == symbol);

        syncSettingAfter.LastSync.ShouldBeGreaterThan(lastSyncBefore);
        syncSettingAfter.LastSync.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }

    #region Helper Methods

    private async Task SetupExchangeAndSyncSettings(string symbol, DateTime startDate)
    {
        var exchangeSetting = new ExchangeSetting
        {
            UserId = _currentUser.Id,
            ExchangeName = ExchangeName.KuCoin,
            ApiKey = "test-api-key",
            Secret = "test-secret",
            Passphrase = "test-passphrase"
        };

        var syncSetting = new SyncSetting
        {
            UserId = _currentUser.Id,
            Symbol = symbol,
            StartSync = startDate,
            LastSync = DateTime.UtcNow
        };

        _context.ExchangeSettings.Add(exchangeSetting);
        _context.SyncSettings.Add(syncSetting);
        await _context.SaveChangesAsync(CancellationToken.None);
    }

    private List<Lib.ExternalServices.KuCoin.Models.TradeHistory> CreateMockTrades(
        string symbol,
        DateTime startDate,
        int count)
    {
        var trades = new List<Lib.ExternalServices.KuCoin.Models.TradeHistory>();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            var price = 100m + random.Next(1, 50);
            var size = 1m + (decimal)random.NextDouble();
            var fee = price * size * 0.001m;
            var side = i % 2 == 0 ? "buy" : "sell";
            var tradedAt = startDate.AddHours(i * 6);

            trades.Add(CreateMockTrade(symbol, side, price, size, fee, tradedAt));
        }

        return trades;
    }

    private Lib.ExternalServices.KuCoin.Models.TradeHistory CreateMockTrade(
        string symbol,
        string side,
        decimal price,
        decimal size,
        decimal fee,
        DateTime? tradedAt = null)
    {
        var tradeId = Guid.NewGuid().GetHashCode();
        var funds = price * size;
        var timestamp = tradedAt ?? DateTime.UtcNow;

        return new Lib.ExternalServices.KuCoin.Models.TradeHistory
        {
            Symbol = symbol,
            TradeId = Math.Abs(tradeId).ToString(),
            OrderId = Guid.NewGuid().ToString(),
            CounterOrderId = Guid.NewGuid().ToString(),
            Side = side,
            Liquidity = "taker",
            ForceTaker = true,
            Price = price.ToString(),
            Size = size.ToString(),
            Funds = funds.ToString(),
            Fee = fee.ToString(),
            FeeRate = "0.001",
            FeeCurrency = "USDT",
            Stop = string.Empty,
            TradeType = "TRADE",
            Type = "limit",
            CreatedAt = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds()
        };
    }

    private Lib.ExternalServices.KuCoin.Models.TradeHistory CreateMockTradeWithId(
        string symbol,
        string side,
        decimal price,
        decimal size,
        decimal fee,
        long tradeId,
        DateTime? tradedAt = null)
    {
        var funds = price * size;
        var timestamp = tradedAt ?? DateTime.UtcNow;

        return new Lib.ExternalServices.KuCoin.Models.TradeHistory
        {
            Symbol = symbol,
            TradeId = tradeId.ToString(),
            OrderId = Guid.NewGuid().ToString(),
            CounterOrderId = Guid.NewGuid().ToString(),
            Side = side,
            Liquidity = "taker",
            ForceTaker = true,
            Price = price.ToString(),
            Size = size.ToString(),
            Funds = funds.ToString(),
            Fee = fee.ToString(),
            FeeRate = "0.001",
            FeeCurrency = "USDT",
            Stop = string.Empty,
            TradeType = "TRADE",
            Type = "limit",
            CreatedAt = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds()
        };
    }

    private void SetupKuCoinServiceMock(List<Lib.ExternalServices.KuCoin.Models.TradeHistory> trades)
    {
        _kuCoinServiceMock.Setup(x => x.GetTradeHistory(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<KuCoinConfig>()))
            .ReturnsAsync(new TradeHistoryResponse { Items = trades });
    }

    #endregion
}
