using System.Collections.Concurrent;
using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refit;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public class TradeSpotGridCommand : IRequest
    {
    }

    public class TradeSpotGridCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        INotifier notifier)
        : IRequestHandler<TradeSpotGridCommand>
    {
        private readonly ConcurrentDictionary<string, Kline> _spotPrice = new();

        public async Task Handle(TradeSpotGridCommand command, CancellationToken cancellationToken)
        {
            var spotGrids = await cexDbContext.SpotGrids
                .Include(x => x.GridSteps)
                .ToListAsync(cancellationToken);
            var symbols = spotGrids.Select(x => x.Symbol).Distinct().ToArray();
            logTrace.LogDebug(new { SpotGrids = spotGrids.Count, Symbols = symbols.Length });
            if (symbols.Length == 0)
            {
                return;
            }

            // Get all prices
            await GetPrices(symbols);

            // 1. Handle SpotGridStatus.NEW
            //   1.1 Change SpotGridStatus.RUNNING if the market price has reached the TriggerPrice  
            // 2. Handle SpotGridStatus.RUNNING
            //   2.1 Check AwaitingBuy
            //   2.2 Check BuyOrderPlaced
            //   2.2 Check AwaitingSell
            //   2.2 Check SellOrderPlaced
            // HandleStatusNew(spotGrids.Where(x => x.Status == SpotGridStatus.NEW).ToList());
            // await HandleStatusRunning(spotGrids.Where(x => x.Status == SpotGridStatus.RUNNING).ToList());


            await cexDbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task GetPrices(string[] symbols)
        {
            var tasks = symbols.Select(async symbol =>
            {
                var res = await kuCoinService.GetKlines(symbol, "5min",
                    DateTime.Now.AddHours(-1), DateTime.Now, kuCoinConfig.Value);
                var curr = res.First();
                _spotPrice.TryAdd(symbol, curr);
            });

            await Task.WhenAll(tasks);
        }

        private void HandleStatusNew(List<SpotGrid> grids)
        {
            foreach (var grid in from grid in grids
                     let lowestPrice = _spotPrice[grid.Symbol].LowestPrice
                     where grid.TriggerPrice >= lowestPrice
                     select grid)
            {
                grid.Status = SpotGridStatus.RUNNING;
                cexDbContext.SpotGrids.Update(grid);
            }
        }

        private async Task HandleStatusRunning(List<SpotGrid> grids)
        {
            var tasks = grids.Select(grid =>
            {
                var lowestPrice = _spotPrice[grid.Symbol].LowestPrice;
                var lowestPriceThreshold = lowestPrice * (decimal)0.9;
                var highestPrice = _spotPrice[grid.Symbol].HighestPrice;
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

                return Task.WhenAll(
                    Task.WhenAll(awaitingBuySteps.Select(step => ChangeStepStatusToBuyOrderPlaced(grid, step))),
                    Task.WhenAll(buyOrderPlacedSteps.Select(step => ChangeStepStatusToAwaitingSell(grid, step))),
                    Task.WhenAll(awaitingSellSteps.Select(step => ChangeStepStatusToSellOrderPlaced(grid, step))),
                    Task.WhenAll(sellOrderPlacedSteps.Select(step => ChangeStepStatusToAwaitingBuy(grid, step))));
            });

            await Task.WhenAll(tasks);
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
            var alertMessage =
                $"Bot {grid.Id}: Buy {FormatPrice(step.Qty)} {symbols[0]} for {FormatPrice(step.Qty * step.BuyPrice)} {symbols[1]} ({grid.Symbol})"; //BotId: {grid.Id}, Buy {grid.Symbol}: {step.Qty}-{step.Qty * step.BuyPrice}";
            try
            {
                var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
                step.OrderId = orderId;
                step.Status = SpotGridStepStatus.BuyOrderPlaced;
                grid.QuoteBalance -= step.Qty * step.BuyPrice;

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

        private async Task HandleTakeProfit(List<SpotGrid> grids)
        {
            foreach (var grid in from grid in grids
                     let closePrice = _spotPrice[grid.Symbol].ClosePrice
                     where grid.TakeProfit != null && grid.TakeProfit <= closePrice
                     select grid)
            {
                grid.Status = SpotGridStatus.TAKE_PROFIT;

                foreach (var step in grid.GridSteps)
                {
                    if (string.IsNullOrWhiteSpace(step.OrderId))
                    {
                        continue;
                    }

                    var res = await kuCoinService.CancelOrder(step.OrderId, kuCoinConfig.Value);
                    logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
                }

                var orderReq = new OrderRequest
                {
                    Symbol = grid.Symbol,
                    Side = "sell",
                    Type = "market",
                    Size = grid.BaseBalance.ToString(CultureInfo.InvariantCulture)
                };
                var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
                var order = await kuCoinService.GetOrderDetails(orderId, kuCoinConfig.Value);

                var alertMessage = $"Take Profit {orderId}";
                logTrace.LogInformation(alertMessage, order);
                await notifier.NotifyInfo(alertMessage, orderReq);

                cexDbContext.SpotGrids.Update(grid);
            }
        }

        private async Task HandleStopLoss(List<SpotGrid> grids)
        {
            foreach (var grid in from grid in grids
                     let closePrice = _spotPrice[grid.Symbol].ClosePrice
                     where grid.TakeProfit != null && grid.TakeProfit <= closePrice
                     select grid)
            {
                grid.Status = SpotGridStatus.STOP_LOSS;

                foreach (var step in grid.GridSteps)
                {
                    if (string.IsNullOrWhiteSpace(step.OrderId))
                    {
                        continue;
                    }

                    var res = await kuCoinService.CancelOrder(step.OrderId, kuCoinConfig.Value);
                    logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
                }

                var orderReq = new OrderRequest
                {
                    Symbol = grid.Symbol,
                    Side = "sell",
                    Type = "market",
                    Size = grid.BaseBalance.ToString(CultureInfo.InvariantCulture)
                };
                var orderId = await kuCoinService.PlaceOrder(orderReq, kuCoinConfig.Value);
                var order = await kuCoinService.GetOrderDetails(orderId, kuCoinConfig.Value);

                var alertMessage = $"Stop Loss {orderId}";
                logTrace.LogInformation(alertMessage, order);
                await notifier.NotifyInfo(alertMessage, orderReq);

                cexDbContext.SpotGrids.Update(grid);
            }
        }

        private string FormatPrice(decimal price)
        {
            var formatted = price.ToString("G29", CultureInfo.InvariantCulture);
            if (formatted.Contains('E'))
            {
                formatted = price.ToString("F10").TrimEnd('0').TrimEnd('.');
            }

            return formatted;
        }
    }
}