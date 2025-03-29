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
    public record RunCommand(SpotGrid Grid, Kline Kline) : IRequest
    {
    }

    public class RunCommandHandler(
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        IPublisher publisher)
        : IRequestHandler<RunCommand>
    {
        public async Task Handle(RunCommand command, CancellationToken cancellationToken)
        {
            var (grid, kline) = command;
            var (lowestPrice, highestPrice) = (kline.LowestPrice, kline.HighestPrice);

            var steps = grid.GridSteps
                .Where(step => step.Type == SpotGridStepType.Normal)
                .ToLookup(step => step.Status);

            var tasks = new List<Task>();

            if (steps.Contains(SpotGridStepStatus.AwaitingBuy))
            {
                tasks.AddRange(steps[SpotGridStepStatus.AwaitingBuy]
                    .Where(step => IsPriceWithinThreshold(step.BuyPrice, lowestPrice, highestPrice))
                    .Select(step => AwaitingBuyStep(grid, step)));
            }

            if (steps.Contains(SpotGridStepStatus.BuyOrderPlaced))
            {
                tasks.AddRange(steps[SpotGridStepStatus.BuyOrderPlaced]
                    .Where(step => !string.IsNullOrWhiteSpace(step.OrderId))
                    .Select(step => BuyOrderPlacedStep(grid, step)));
            }

            if (steps.Contains(SpotGridStepStatus.AwaitingSell))
            {
                tasks.AddRange(steps[SpotGridStepStatus.AwaitingSell]
                    .Where(step => IsPriceWithinThreshold(step.SellPrice, lowestPrice, highestPrice))
                    .Select(step => AwaitingSellStep(grid, step)));
            }

            if (steps.Contains(SpotGridStepStatus.SellOrderPlaced))
            {
                tasks.AddRange(steps[SpotGridStepStatus.SellOrderPlaced]
                    .Where(step => !string.IsNullOrWhiteSpace(step.OrderId))
                    .Select(step => SellOrderPlacedStep(grid, step)));
            }

            await Task.WhenAll(tasks);
            await cexDbContext.SaveChangesAsync(cancellationToken);
        }

        private static bool IsPriceWithinThreshold(decimal price, decimal lowestPrice, decimal highestPrice)
        {
            var threshold = price * 0.05m;
            return Math.Abs(lowestPrice - price) <= threshold || Math.Abs(highestPrice - price) <= threshold;
        }

        private async Task AwaitingBuyStep(SpotGrid grid, SpotGridStep step)
        {
            try
            {
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

                await publisher.Publish(new PlaceOrderNotification(grid, orderReq));
            }
            catch (Exception ex)
            {
                await publisher.Publish(new PlaceOrderNotification(grid, ex));
            }
        }

        private async Task BuyOrderPlacedStep(SpotGrid grid, SpotGridStep step)
        {
            try
            {
                var orderDetails = await kuCoinService.GetOrderDetails(step.OrderId ?? "",
                    kuCoinConfig.Value);
                if (!orderDetails.IsActive)
                {
                    step.AddOrderDetails(grid, SpotGridStepStatus.AwaitingSell, orderDetails);

                    grid.BaseBalance += step.Qty;

                    await publisher.Publish(new FillOrderNotification(grid, orderDetails));
                }
            }
            catch (Exception ex)
            {
                await publisher.Publish(new FillOrderNotification(grid, ex));
            }
        }

        private async Task AwaitingSellStep(SpotGrid grid, SpotGridStep step)
        {
            try
            {
                var orderReq = new PlaceOrderRequest
                {
                    Symbol = grid.Symbol,
                    Side = "sell",
                    Type = "limit",
                    Price = step.SellPrice.ToString(CultureInfo.InvariantCulture),
                    Size = step.Qty.ToString(CultureInfo.InvariantCulture)
                };

                var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
                step.OrderId = orderId;
                step.Status = SpotGridStepStatus.SellOrderPlaced;

                grid.BaseBalance -= step.Qty;

                await publisher.Publish(new PlaceOrderNotification(grid, orderReq));
            }
            catch (Exception ex)
            {
                await publisher.Publish(new PlaceOrderNotification(grid, ex));
            }
        }

        private async Task SellOrderPlacedStep(SpotGrid grid, SpotGridStep step)
        {
            try
            {
                var orderDetails = await kuCoinService.GetOrderDetails(step.OrderId ?? "",
                    kuCoinConfig.Value);
                var executedQuantity = decimal.Parse(orderDetails.DealSize);

                if (!orderDetails.IsActive && executedQuantity > 0)
                {
                    step.AddOrderDetails(grid, SpotGridStepStatus.AwaitingBuy, orderDetails);

                    grid.Profit = (step.SellPrice - step.BuyPrice) * step.Qty;
                    grid.QuoteBalance = (grid.QuoteBalance + step.Qty * step.SellPrice).FixedNumber();
                    await publisher.Publish(new FillOrderNotification(grid, orderDetails));
                }
            }
            catch (Exception ex)
            {
                await publisher.Publish(new FillOrderNotification(grid, ex));
            }
        }
    }
}