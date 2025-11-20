using Cex.Application.KuCoin.Commands.SyncTradeHistory;
using MediatR;

namespace WebAPI.HostedServices
{
    public class SyncTradeHistoryService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SyncTradeHistoryService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Factory.StartNew(async () =>
            {
                logger.LogInformation("Start {serviceName} started at {startAt}", GetType().Name, DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceScopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        await mediator.Send(new SyncTradeHistoryCommand(new DateTime(2025, 9, 20)), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Exception {serviceName}", GetType().Name);
                    }

                    await Task.Delay(5 * 60 * 1000, stoppingToken);
                }
            }, stoppingToken);
        }
    }
}