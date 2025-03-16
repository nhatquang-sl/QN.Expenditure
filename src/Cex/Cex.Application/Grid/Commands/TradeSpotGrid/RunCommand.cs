using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;
using Refit;

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
        INotifier notifier)
        : IRequestHandler<RunCommand>
    {
        public async Task Handle(RunCommand command, CancellationToken cancellationToken)
        {
            var grid = command.Grid;
            var kline = command.Kline;
            var lowestPrice = kline.LowestPrice;
            var lowestPriceThreshold = lowestPrice * (decimal)0.9;
            var highestPrice = kline.HighestPrice;
            var highestPriceThreshold = highestPrice * (decimal)1.1;
            var awaitingBuySteps = grid.GridSteps
                .Where(step => step.Status == SpotGridStepStatus.AwaitingBuy
                               && step.BuyPrice <= lowestPrice
                               && step.BuyPrice >= lowestPriceThreshold)
                .ToList();

            var buyOrderPlacedSteps = grid.GridSteps
                .Where(step => step.Status == SpotGridStepStatus.BuyOrderPlaced
                               && !string.IsNullOrWhiteSpace(step.OrderId))
                .ToList();

            var awaitingSellSteps = grid.GridSteps
                .Where(step => step.Status == SpotGridStepStatus.AwaitingSell
                               && step.SellPrice >= highestPrice
                               && step.SellPrice <= highestPriceThreshold)
                .ToList();

            var sellOrderPlacedSteps = grid.GridSteps
                .Where(step => step.Status == SpotGridStepStatus.SellOrderPlaced
                               && !string.IsNullOrWhiteSpace(step.OrderId))
                .ToList();

            await Task.WhenAll(
                Task.WhenAll(awaitingBuySteps.Select(step => ChangeStepStatusToBuyOrderPlaced(grid, step))),
                Task.WhenAll(buyOrderPlacedSteps.Select(step => ChangeStepStatusToAwaitingSell(grid, step))),
                Task.WhenAll(awaitingSellSteps.Select(step => ChangeStepStatusToSellOrderPlaced(grid, step))),
                Task.WhenAll(sellOrderPlacedSteps.Select(step => ChangeStepStatusToAwaitingBuy(grid, step))));
        }

        private async Task ChangeStepStatusToBuyOrderPlaced(SpotGrid grid, SpotGridStep step)
        {
            var orderReq = new OrderRequest
            {
                Symbol = grid.Symbol,
                Side = "buy",
                Type = "limit",
                Price = step.BuyPrice.ToString(CultureInfo.InvariantCulture),
                Size = step.Qty.ToString(CultureInfo.InvariantCulture)
            };
            var symbols = grid.Symbol.Split('-');
            //BotId: {grid.Id}, Buy {grid.Symbol}: {step.Qty}-{step.Qty * step.BuyPrice}";
            var alertMessage =
                $"Bot {grid.Id}: " +
                $"Buy {step.Qty.FormatPrice()} {symbols[0]} " +
                $"for {(step.Qty * step.BuyPrice).FormatPrice()} {symbols[1]} ({grid.Symbol})";
            try
            {
                var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
                step.OrderId = orderId;
                step.Status = SpotGridStepStatus.BuyOrderPlaced;
                grid.QuoteBalance = (grid.QuoteBalance - step.Qty * step.BuyPrice).FixedNumber();

                await notifier.NotifyInfo(alertMessage, orderReq);
            }
            catch (Exception ex)
            {
                await notifier.NotifyError(alertMessage, ex);
            }
        }

        private async Task ChangeStepStatusToAwaitingSell(SpotGrid grid, SpotGridStep step)
        {
            try
            {
                var orderDetails = await kuCoinService.GetOrderDetails(step.OrderId ?? "",
                    kuCoinConfig.Value);
                var executedQuantity = decimal.Parse(orderDetails.DealSize);
                if (!orderDetails.IsActive && executedQuantity > 0)
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
                }
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (ex is ApiException exception)
                {
                    message = exception.Content ?? exception.Message;
                }

                await notifier.NotifyError(message, ex);
            }
        }

        private async Task ChangeStepStatusToSellOrderPlaced(SpotGrid grid, SpotGridStep step)
        {
            try
            {
                var buyOrder = step.Orders
                    .Where(x => x.Side == "buy" && x.Price == step.BuyPrice)
                    .OrderBy(x => x.CreatedAt)
                    .LastOrDefault();
                if (buyOrder == null)
                {
                    return;
                }

                var orderId = await kuCoinService.PlaceOrder(new OrderRequest
                    {
                        Symbol = grid.Symbol,
                        Side = "sell",
                        Type = "limit",
                        Price = step.SellPrice.ToString(CultureInfo.InvariantCulture),
                        Size = buyOrder.OrigQty.ToString(CultureInfo.InvariantCulture)
                    },
                    kuCoinConfig.Value);
                step.OrderId = orderId;
                step.Status = SpotGridStepStatus.SellOrderPlaced;
                grid.BaseBalance -= step.Qty;
            }
            catch (Exception ex)
            {
                logTrace.LogError("ChangeStepStatusToSellOrderPlaced()", ex);
                await notifier.NotifyError(ex.Message, ex);
            }
        }

        private async Task ChangeStepStatusToAwaitingBuy(SpotGrid grid, SpotGridStep step)
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
                }
            }
            catch (Exception ex)
            {
                await notifier.NotifyError(ex.Message, ex);
            }
        }
    }
}