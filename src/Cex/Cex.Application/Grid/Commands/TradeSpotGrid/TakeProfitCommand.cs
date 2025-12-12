using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Shared.Extensions;
using Cex.Domain.Entities;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
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
        IPublisher publisher)
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
                var res = await kuCoinService.CancelOrder(step.OrderId!, kuCoinConfig.Value);
                logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
                step.OrderId = null;
            }
        }

        private async Task PlaceOrder(SpotGrid grid, CancellationToken cancellationToken)
        {
            var orderReq = new PlaceOrderRequest
            {
                Symbol = grid.Symbol,
                Side = "sell",
                Type = "market",
                Size = grid.BaseBalance.ToString(CultureInfo.InvariantCulture)
            };
            var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
            var orderDetails = await kuCoinService.GetOrderDetails(orderId, kuCoinConfig.Value);

            grid.GridSteps.First(x => x.Type == SpotGridStepType.TakeProfit)
                .AddOrderDetails(grid, SpotGridStepStatus.SellOrderPlaced, orderDetails);

            await publisher.Publish(new FillOrderNotification(grid, orderDetails), cancellationToken);
        }
    }
}