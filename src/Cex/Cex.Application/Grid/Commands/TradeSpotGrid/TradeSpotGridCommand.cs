using System.Collections.Concurrent;
using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public class TradeSpotGridCommand : IRequest
    {
    }

    public class TradeSpotGridCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig)
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
            HandleStatusNew(spotGrids.Where(x => x.Status == SpotGridStatus.NEW).ToList());
            await HandleStatusRunning(spotGrids.Where(x => x.Status == SpotGridStatus.RUNNING).ToList());

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
                     where grid.TriggerPrice <= lowestPrice
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
                return Task.WhenAll(ChangeStepStatusToBuyOrderPlaced(grid, awaitingBuySteps),
                    ChangeStepStatusToAwaitingSell(grid, buyOrderPlacedSteps),
                    ChangeStepStatusToSellOrderPlaced(grid, awaitingSellSteps),
                    ChangeStepStatusToAwaitingBuy(grid, sellOrderPlacedSteps));
            });

            await Task.WhenAll(tasks);
        }

        private async Task ChangeStepStatusToBuyOrderPlaced(SpotGrid grid, List<SpotGridStep> gridSteps)
        {
            var tasks = gridSteps
                .Select(async step =>
                {
                    var orderId = await kuCoinService.PlaceOrder(new OrderRequest
                        {
                            Symbol = grid.Symbol,
                            Side = "buy",
                            Type = "limit",
                            Price = step.BuyPrice.ToString(CultureInfo.InvariantCulture),
                            Size = step.Qty.ToString(CultureInfo.InvariantCulture)
                        },
                        kuCoinConfig.Value);
                    step.OrderId = orderId;
                    step.Status = SpotGridStepStatus.BuyOrderPlaced;
                });
            await Task.WhenAll(tasks);
        }

        private async Task ChangeStepStatusToAwaitingSell(SpotGrid grid, List<SpotGridStep> gridSteps)
        {
            var tasks = gridSteps
                .Select(async step =>
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
                    }
                });

            await Task.WhenAll(tasks);
        }

        private async Task ChangeStepStatusToSellOrderPlaced(SpotGrid grid, List<SpotGridStep> gridSteps)
        {
            var tasks = gridSteps
                .Select(async step =>
                {
                    var buyOrder = step.Orders
                        .Where(x => x.Side == "buy" && x.Price == step.BuyPrice)
                        .OrderBy(x => x.CreatedAt)
                        .Last();
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
                });
            await Task.WhenAll(tasks);
        }

        private async Task ChangeStepStatusToAwaitingBuy(SpotGrid grid, List<SpotGridStep> gridSteps)
        {
            var tasks = gridSteps
                .Select(async step =>
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
                    }
                });

            await Task.WhenAll(tasks);
        }
    }
}