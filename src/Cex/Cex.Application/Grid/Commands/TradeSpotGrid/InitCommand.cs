using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record InitCommand(SpotGrid Grid, Kline Kline) : IRequest
    {
    }

    public class InitCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        INotifier notifier)
        : IRequestHandler<InitCommand>
    {
        public async Task Handle(InitCommand command, CancellationToken cancellationToken)
        {
            var grid = command.Grid;
            var kline = command.Kline;
            var step = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);

            switch (step.Status)
            {
                case SpotGridStepStatus.AwaitingBuy:
                    var lowestPrice = kline.LowestPrice;
                    var triggerPriceThreshold = step.BuyPrice * 1.1m;
                    if (lowestPrice > triggerPriceThreshold)
                    {
                        return;
                    }

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

                    cexDbContext.SpotGrids.Update(grid);
                    await cexDbContext.SaveChangesAsync(cancellationToken);
                    await NotifyBuyOrderPlaced(grid, orderReq, cancellationToken);
                    break;
                case SpotGridStepStatus.BuyOrderPlaced:
                    if (string.IsNullOrWhiteSpace(step.OrderId))
                    {
                        return;
                    }

                    var orderDetails = await kuCoinService.GetOrderDetails(step.OrderId, kuCoinConfig.Value);
                    if (orderDetails.IsActive)
                    {
                        return;
                    }

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
                    grid.Status = SpotGridStatus.RUNNING;

                    cexDbContext.SpotGrids.Update(grid);
                    await cexDbContext.SaveChangesAsync(cancellationToken);

                    break;
                case SpotGridStepStatus.AwaitingSell:
                case SpotGridStepStatus.SellOrderPlaced:
                default:
                    break;
            }
        }

        private async Task NotifyBuyOrderPlaced(SpotGrid grid, OrderRequest orderReq,
            CancellationToken cancellationToken)
        {
            var step = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);

            const string quoteCurrency = "USDT";
            var symbols = new[] { grid.Symbol.Replace(quoteCurrency, ""), quoteCurrency };
            //BotId: {grid.Id}, Buy {grid.Symbol}: {step.Qty}-{step.Qty * step.BuyPrice}";
            var alertMessage =
                $"Bot {grid.Id}: Buy {step.Qty.FormatPrice()} {symbols[0]} for {(step.Qty * step.BuyPrice).FormatPrice()} {symbols[1]} ({grid.Symbol})";
            await notifier.NotifyInfo(alertMessage, orderReq, cancellationToken);
        }
    }
}