using System.Collections.Concurrent;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        IOptions<KuCoinConfig> kuCoinConfig,
        IServiceScopeFactory scopeFactory)
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

            await HandleInitialStep(spotGrids, cancellationToken);
            await HandleStatusRunning(spotGrids, cancellationToken);
            await HandleTakeProfit(spotGrids, cancellationToken);
            await HandleStopLoss(spotGrids, cancellationToken);
            await cexDbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task GetPrices(string[] symbols)
        {
            var tasks = symbols.Select(async symbol =>
            {
                var res = await kuCoinService.GetKlines(symbol, "5min",
                    DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, kuCoinConfig.Value);
                var curr = res.Last();
                _spotPrice.TryAdd(symbol, curr);
            });

            await Task.WhenAll(tasks);
        }

        private async Task HandleInitialStep(List<SpotGrid> grids, CancellationToken cancellationToken)
        {
            var tasks = grids
                .Where(g => g.GridSteps.Any(s =>
                    s is
                    {
                        Type: SpotGridStepType.Initial,
                        Status: SpotGridStepStatus.AwaitingBuy or SpotGridStepStatus.BuyOrderPlaced
                    }))
                .ToList()
                .Select(async grid =>
                {
                    using var scope = scopeFactory.CreateScope();
                    // Resolve services within the new scope
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                    await sender.Send(new InitCommand(grid, _spotPrice[grid.Symbol]),
                        cancellationToken);
                });
            await Task.WhenAll(tasks);
        }

        private async Task HandleStatusRunning(List<SpotGrid> grids, CancellationToken cancellationToken)
        {
            var tasks = grids.Select(async grid =>
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                await sender.Send(new RunCommand(grid, _spotPrice[grid.Symbol]),
                    cancellationToken);
            });

            await Task.WhenAll(tasks);
        }

        private async Task HandleTakeProfit(List<SpotGrid> grids, CancellationToken cancellationToken)
        {
            var tasks = grids
                .Where(g => g is { Status: SpotGridStatus.RUNNING, TakeProfit: not null }
                            && g.TakeProfit > _spotPrice[g.Symbol].ClosePrice
                )
                .ToList()
                .Select(async grid =>
                {
                    using var scope = scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                    await sender.Send(new TakeProfitCommand(grid, _spotPrice[grid.Symbol]),
                        cancellationToken);
                });
            await Task.WhenAll(tasks);
        }

        private async Task HandleStopLoss(List<SpotGrid> grids, CancellationToken cancellationToken)
        {
            var tasks = grids
                .Where(g => g is { Status: SpotGridStatus.RUNNING, StopLoss: not null }
                            && g.StopLoss > _spotPrice[g.Symbol].ClosePrice
                )
                .ToList()
                .Select(async grid =>
                {
                    using var scope = scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                    await sender.Send(new StopLossCommand(grid, _spotPrice[grid.Symbol]),
                        cancellationToken);
                });
            await Task.WhenAll(tasks);
        }
    }
}