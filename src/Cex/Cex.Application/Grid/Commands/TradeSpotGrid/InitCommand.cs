using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Shared.Extensions;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record InitCommand(SpotGrid Grid, Kline Kline) : IRequest
    {
    }

    public class InitCommandHandler(
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        IPublisher publisher)
        : IRequestHandler<InitCommand>
    {
        public async Task Handle(InitCommand command, CancellationToken cancellationToken)
        {
            var (grid, kline) = command;
            var step = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);

            switch (step.Status)
            {
                case SpotGridStepStatus.AwaitingBuy:
                    await ProcessAwaitingBuy(grid, step, kline, cancellationToken);
                    break;
                case SpotGridStepStatus.BuyOrderPlaced:
                    await ProcessBuyOrderPlaced(grid, step, cancellationToken);
                    break;
                case SpotGridStepStatus.AwaitingSell:
                case SpotGridStepStatus.SellOrderPlaced:
                default:
                    break;
            }
        }

        private async Task ProcessAwaitingBuy(SpotGrid grid, SpotGridStep step, Kline kline,
            CancellationToken cancellationToken)
        {
            var triggerPriceThreshold = step.BuyPrice * 1.1m;
            if (kline.LowestPrice > triggerPriceThreshold)
            {
                return;
            }

            var orderReq = new PlaceOrderRequest
            {
                Symbol = grid.Symbol,
                Side = "buy",
                Type = "limit",
                Price = step.BuyPrice.ToString(CultureInfo.InvariantCulture),
                Size = step.Qty.ToString(CultureInfo.InvariantCulture)
            };

            var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
            step.OrderId = orderId;
            step.Status = SpotGridStepStatus.BuyOrderPlaced;
            grid.QuoteBalance = (grid.QuoteBalance - step.Qty * step.BuyPrice).FixedNumber();

            cexDbContext.SpotGrids.Update(grid);
            await cexDbContext.SaveChangesAsync(cancellationToken);
            await publisher.Publish(new PlaceOrderNotification(grid, orderReq), cancellationToken);
        }

        private async Task ProcessBuyOrderPlaced(SpotGrid grid, SpotGridStep step, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(step.OrderId))
            {
                return;
            }

            var orderDetails = await kuCoinService.GetOrderDetails(step.OrderId, kuCoinConfig.Value);
            if (orderDetails.IsActive)
            {
                return;
            }

            step.AddOrderDetails(grid, SpotGridStepStatus.AwaitingSell, orderDetails);

            grid.BaseBalance += step.Qty;
            grid.Status = SpotGridStatus.RUNNING;

            cexDbContext.SpotGrids.Update(grid);
            await cexDbContext.SaveChangesAsync(cancellationToken);
            await publisher.Publish(new FillOrderNotification(grid, orderDetails), cancellationToken);
        }
    }
}