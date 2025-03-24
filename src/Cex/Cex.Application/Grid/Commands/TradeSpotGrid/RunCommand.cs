using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record RunCommand(SpotGrid Grid, Kline Kline) : IRequest
    {
    }

    public class RunCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        IPublisher publisher)
        : IRequestHandler<RunCommand>
    {
        public async Task Handle(RunCommand command, CancellationToken cancellationToken)
        {
            var grid = command.Grid;
            var kline = command.Kline;
            var lowestPrice = kline.LowestPrice;
            var highestPrice = kline.HighestPrice;

            var awaitingBuySteps = grid.GridSteps
                .Where(step => step is { Type: SpotGridStepType.Normal, Status: SpotGridStepStatus.AwaitingBuy }
                               && IsPriceWithinThreshold(step.BuyPrice, lowestPrice, highestPrice))
                .ToList();

            var buyOrderPlacedSteps = grid.GridSteps
                .Where(step => step is { Type: SpotGridStepType.Normal, Status: SpotGridStepStatus.BuyOrderPlaced }
                               && !string.IsNullOrWhiteSpace(step.OrderId))
                .ToList();

            var awaitingSellSteps = grid.GridSteps
                .Where(step => step is { Type: SpotGridStepType.Normal, Status: SpotGridStepStatus.AwaitingSell }
                               && IsPriceWithinThreshold(step.SellPrice, lowestPrice, highestPrice))
                .ToList();

            var sellOrderPlacedSteps = grid.GridSteps
                .Where(step => step is { Type: SpotGridStepType.Normal, Status: SpotGridStepStatus.SellOrderPlaced }
                               && !string.IsNullOrWhiteSpace(step.OrderId))
                .ToList();

            await Task.WhenAll(
                Task.WhenAll(awaitingBuySteps.Select(step => AwaitingBuyStep(grid, step))),
                Task.WhenAll(buyOrderPlacedSteps.Select(step => BuyOrderPlacedStep(grid, step))),
                Task.WhenAll(awaitingSellSteps.Select(step => AwaitingSellStep(grid, step))),
                Task.WhenAll(sellOrderPlacedSteps.Select(step => SellOrderPlacedStep(grid, step))));

            await cexDbContext.SaveChangesAsync(cancellationToken);
        }

        private static bool IsPriceWithinThreshold(decimal price, decimal lowestPrice, decimal highestPrice)
        {
            var lowerThreshold = price * 0.95m;
            var upperThreshold = price * 1.05m;
            if (lowerThreshold <= lowestPrice && lowestPrice <= upperThreshold)
            {
                return true;
            }

            return lowerThreshold <= highestPrice && highestPrice <= upperThreshold;
        }

        private async Task AwaitingBuyStep(SpotGrid grid, SpotGridStep step)
        {
            try
            {
                var orderReq = new OrderRequest
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
                    step.OrderId = null;
                    step.Status = SpotGridStepStatus.AwaitingSell;
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
                var orderReq = new OrderRequest
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
                logTrace.LogError("ChangeStepStatusToSellOrderPlaced()", ex);
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
                    step.OrderId = null;
                    step.Status = SpotGridStepStatus.AwaitingBuy;
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