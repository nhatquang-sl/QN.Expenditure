using Domain.Entities;

namespace Infrastructure.IntegrationTests.BnbSpotOrder
{
    public static class SpotOrderData
    {
        public static SpotOrder Generate(string userId, string symbol, DateTime updateTime)
        {
            return new SpotOrder
            {
                UserId = userId,
                ClientOrderId = "ClientOrderId",
                CummulativeQuoteQty = 0,
                ExecutedQty = 0,
                IcebergQty = 0,
                IsWorking = true,
                OrderId = 1,
                OrderListId = 1,
                OrigQty = 0,
                OrigQuoteOrderQty = 0,
                Price = 0,
                SelfTradePreventionMode = "Mode",
                Side = "Side",
                Status = "Status",
                StopPrice = 0,
                Symbol = symbol,
                Time = updateTime,
                TimeInForce = "GMT",
                Type = "Type",
                WorkingTime = updateTime,
                UpdateTime = updateTime
            };
        }
    }
}
