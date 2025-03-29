using System.Globalization;
using Cex.Domain.Entities;
using Lib.ExternalServices.KuCoin.Models;

namespace Cex.Infrastructure.IntegrationTests.Grid
{
    public static class DataGenerator
    {
        public const decimal BtcLowestPrice = 100;
        public const decimal BtcHighestPrice = 110;

        public static SpotGrid NewSpotGrid(long gridId, string symbol, decimal triggerPrice)
        {
            return new SpotGrid
            {
                Id = gridId,
                UserId = "1",
                Symbol = symbol,
                LowerPrice = triggerPrice * (decimal)0.9,
                UpperPrice = triggerPrice * (decimal)1.1,
                TriggerPrice = triggerPrice,
                NumberOfGrids = 10,
                Investment = 100,
                Status = SpotGridStatus.NEW,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static SpotGridStep GridStep(int id, decimal buyPrice, decimal sellPrice,
            SpotGridStepStatus status, string? orderId = null)
        {
            return new SpotGridStep
            {
                Id = id,
                BuyPrice = buyPrice,
                SellPrice = sellPrice,
                Status = status,
                OrderId = orderId
            };
        }

        public static List<OrderDetails> KuCoinOrderDetails()
        {
            var orderDetails = new List<OrderDetails>();
            for (var i = 0; i < 4; i++)
            {
                orderDetails.Add(new OrderDetails
                {
                    Id = Guid.NewGuid().ToString(),
                    FeeCurrency = "USDT",
                    Fee = "0.001",
                    IsActive = false,
                    DealSize = "0.01",
                    OpType = "DEAL",
                    Price = (BtcLowestPrice - i).ToString(CultureInfo.InvariantCulture),
                    Side = "buy",
                    Size = "0.01",
                    Symbol = "BTC-USDT",
                    TimeInForce = "GTC",
                    TradeType = "TRADE",
                    Type = "limit",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - i * 1000
                });
            }

            for (var i = 1; i >= 0; i--)
            {
                orderDetails.Add(new OrderDetails
                {
                    Id = Guid.NewGuid().ToString(),
                    FeeCurrency = "USDT",
                    Fee = "0.001",
                    IsActive = false,
                    DealSize = "0.01",
                    OpType = "DEAL",
                    Price = (BtcHighestPrice + i).ToString(CultureInfo.InvariantCulture),
                    Side = "sell",
                    Size = "0.01",
                    Symbol = "BTC-USDT",
                    TimeInForce = "GTC",
                    TradeType = "TRADE",
                    Type = "limit",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - i * 1000
                });
            }

            return orderDetails;
        }
    }
}