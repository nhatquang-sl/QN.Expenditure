using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record StopLossCommand(SpotGrid Grid, Kline Kline) : IRequest
    {
    }

    public class StopLossCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        INotifier notifier)
        : IRequestHandler<StopLossCommand>
    {
        public async Task Handle(StopLossCommand command, CancellationToken cancellationToken)
        {
            var grid = command.Grid;
            var kline = command.Kline;
            if (!grid.StopLoss.HasValue || kline.LowestPrice > grid.StopLoss.Value)
            {
                return;
            }

            grid.Status = SpotGridStatus.STOP_LOSS;
            await CancelOrders(grid.GridSteps.ToList());
            await PlaceOrder(grid, cancellationToken);

            cexDbContext.SpotGrids.Update(grid);
            await cexDbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task CancelOrders(List<SpotGridStep> steps)
        {
            foreach (var step in steps.Where(step => !string.IsNullOrWhiteSpace(step.OrderId)))
            {
                var res = await kuCoinService.CancelOrder(step.OrderId, kuCoinConfig.Value);
                logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
                step.OrderId = null;
            }
        }

        private async Task PlaceOrder(SpotGrid grid, CancellationToken cancellationToken)
        {
            var orderReq = new OrderRequest
            {
                Symbol = grid.Symbol,
                Side = "sell",
                Type = "market",
                Size = grid.BaseBalance.ToString(CultureInfo.InvariantCulture)
            };
            var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
            var orderDetails = await kuCoinService.GetOrderDetails(orderId, kuCoinConfig.Value);

            var stopLossStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.StopLoss);
            stopLossStep.OrderId = orderId;
            stopLossStep.Status = SpotGridStepStatus.SellOrderPlaced;
            stopLossStep.Orders.Add(new SpotOrder
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

            var alertMessage = $"Stop Loss {orderId}";
            logTrace.LogInformation(alertMessage, orderDetails);
            await notifier.NotifyInfo(alertMessage, orderReq, cancellationToken);
        }
    }
}