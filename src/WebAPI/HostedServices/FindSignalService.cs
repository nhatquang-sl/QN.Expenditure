using Cex.Application.Signals.Commands.FindSignal;
using Cex.Application.Signals.Commands.CheckSignalEntry;
using Cex.Application.Signals.Commands.CheckSignalMaxProfit;
using Cex.Application.Signals.Commands.CheckSignalStopLoss;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;

namespace WebAPI.HostedServices;

public class FindSignalService(IServiceScopeFactory serviceScopeFactory, ILogger<FindSignalService> logger)
    : BackgroundService
{
    private async Task FindSignal(IntervalType intervalType, CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new FindSignalCommand(intervalType), stoppingToken);
    }

    private async Task CheckSignalEntry(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new CheckSignalEntryCommand(), stoppingToken);
    }

    private async Task CheckSignalStopLoss(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new CheckSignalStopLossCommand(), stoppingToken);
    }

    private async Task CheckSignalMaxProfit(CancellationToken stoppingToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new CheckSignalMaxProfitCommand(), stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Factory.StartNew(async () =>
        {
            logger.LogInformation("Started at {startAt}", DateTime.UtcNow);
            while (true)
            {
                try
                {

                    if (DateTime.UtcNow.Minute % 5 == 1)
                    {
                        await FindSignal(IntervalType.FiveMinutes, stoppingToken);
                        await CheckSignalEntry(stoppingToken);
                        await CheckSignalStopLoss(stoppingToken);
                        await CheckSignalMaxProfit(stoppingToken);
                    }

                    if (DateTime.UtcNow.Minute % 15 == 1)
                    {
                        await FindSignal(IntervalType.FifteenMinutes, stoppingToken);
                    }

                    if (DateTime.UtcNow.Minute % 30 == 1)
                    {
                        await FindSignal(IntervalType.ThirtyMinutes, stoppingToken);
                    }

                    if (DateTime.UtcNow.Minute == 1)
                    {
                        await FindSignal(IntervalType.OneHour, stoppingToken);
                    }

                    if (DateTime.UtcNow.Hour % 4 == 1 && DateTime.UtcNow.Minute == 1)
                    {
                        await FindSignal(IntervalType.FourHours, stoppingToken);
                    }

                    if (DateTime.UtcNow.Hour == 1 && DateTime.UtcNow.Minute == 1)
                    {
                        await FindSignal(IntervalType.OneDay, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception {serviceName}", GetType().Name);
                }

                await Task.Delay(60 * 1000, stoppingToken);
            }
        }, stoppingToken);
    }
}