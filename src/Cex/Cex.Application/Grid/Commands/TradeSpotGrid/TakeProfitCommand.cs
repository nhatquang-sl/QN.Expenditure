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
    public record TakeProfitCommand(SpotGrid Grid, Kline Kline) : IRequest
    {
    }

    public class TakeProfitCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        INotifier notifier)
        : IRequestHandler<TakeProfitCommand>
    {
        public async Task Handle(TakeProfitCommand command, CancellationToken cancellationToken)
        {
            var grid = command.Grid;
            var kline = command.Kline;
            if (!grid.TakeProfit.HasValue || grid.TakeProfit.Value > kline.ClosePrice)
            {
                return;
            }

            grid.Status = SpotGridStatus.TAKE_PROFIT;

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

            var takeProfitStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.TakeProfit);
            takeProfitStep.OrderId = orderId;
            takeProfitStep.Status = SpotGridStepStatus.SellOrderPlaced;
            takeProfitStep.Orders.Add(new SpotOrder
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

            var alertMessage = $"Take Profit {orderId}";
            logTrace.LogInformation(alertMessage, orderDetails);
            await notifier.NotifyInfo(alertMessage, orderReq, cancellationToken);
        }
    }
}