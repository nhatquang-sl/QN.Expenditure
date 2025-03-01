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

namespace Cex.Application.Grid.Commands.TradeSpotGrid
{
    public record TakeProfitSpotGridCommand(ConcurrentDictionary<string, Kline> SpotPrice) : IRequest
    {
    }

    public class TakeProfitSpotGridCommandHandler(
        ILogTrace logTrace,
        ICexDbContext cexDbContext,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        INotifier notifier)
        : IRequestHandler<TakeProfitSpotGridCommand>
    {
        public async Task Handle(TakeProfitSpotGridCommand request, CancellationToken cancellationToken)
        {
            var grids = await cexDbContext.SpotGrids
                .Where(g => g.Status == SpotGridStatus.RUNNING && g.TakeProfit != null)
                .Include(x => x.GridSteps)
                .ToListAsync(cancellationToken);
            if (grids.Count == 0)
            {
                return;
            }

            foreach (var grid in from grid in grids
                     let closePrice = request.SpotPrice[grid.Symbol].ClosePrice
                     where grid.TakeProfit != null && grid.TakeProfit <= closePrice
                     select grid)
            {
                await TakeProfit(grid);
            }

            await cexDbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task CancelAllSteps(List<SpotGridStep> steps)
        {
            foreach (var step in steps)
            {
                if (step.OrderId is null)
                {
                    continue;
                }

                var res = await kuCoinService.CancelOrder(step.OrderId, kuCoinConfig.Value);
                logTrace.LogInformation($"Cancel order {step.OrderId} of step {step.Id}", res);
            }
        }

        private async Task TakeProfit(SpotGrid grid)
        {
            try
            {
                grid.Status = SpotGridStatus.TAKE_PROFIT;
                await CancelAllSteps(grid.GridSteps.ToList());

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
            }
            catch (Exception ex)
            {
                var alertMessage = $"Take Profit {grid.Symbol}: {ex.Message}";
                logTrace.LogError(alertMessage, ex);
                await notifier.NotifyError(alertMessage, ex);
            }
        }
    }
}