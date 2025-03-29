using Cex.Domain.Entities;
using Lib.ExternalServices.KuCoin.Models;

namespace Cex.Application.Grid.Shared.Extensions
{
    public static class SpotGridStepExtension
    {
        public static void AddOrderDetails(this SpotGridStep step, SpotGrid grid, SpotGridStepStatus status,
            OrderDetails orderDetails)
        {
            step.OrderId = null;
            step.Status = status;
            step.Orders.Add(new SpotOrder
            {
                UserId = grid.UserId,
                Symbol = grid.Symbol,
                OrderId = orderDetails.Id,
                ClientOrderId = orderDetails.ClientOid,
                Price = decimal.Parse(orderDetails.Price),
                OrigQty = decimal.Parse(orderDetails.Size),
                TimeInForce = orderDetails.TimeInForce,
                Type = orderDetails.Type,
                Side = orderDetails.Side,
                Fee = decimal.Parse(orderDetails.Fee),
                FeeCurrency = orderDetails.FeeCurrency,
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(orderDetails.CreatedAt).UtcDateTime,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}