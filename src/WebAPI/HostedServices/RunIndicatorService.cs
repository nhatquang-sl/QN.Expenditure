using Cex.Application.Indicator.Commands;
using Cex.Application.Indicator.Shared;
using MediatR;

namespace WebAPI.HostedServices
{
    public class RunIndicatorService(IServiceScopeFactory serviceScopeFactory, ILogger<RunIndicatorService> logger)
        : BackgroundService
    {
        private async Task RunIndicator(IntervalType intervalType, CancellationToken stoppingToken)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new RunIndicatorCommand(intervalType), stoppingToken);
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
                            await RunIndicator(IntervalType.FiveMinutes, stoppingToken);
                        }

                        if (DateTime.UtcNow.Minute % 15 == 1)
                        {
                            await RunIndicator(IntervalType.FifteenMinutes, stoppingToken);
                        }

                        if (DateTime.UtcNow.Minute % 30 == 1)
                        {
                            await RunIndicator(IntervalType.ThirtyMinutes, stoppingToken);
                        }

                        if (DateTime.UtcNow.Minute == 1)
                        {
                            await RunIndicator(IntervalType.OneHour, stoppingToken);
                        }

                        if (DateTime.UtcNow.Hour % 4 == 1 && DateTime.UtcNow.Minute == 1)
                        {
                            await RunIndicator(IntervalType.FourHours, stoppingToken);
                        }

                        if (DateTime.UtcNow.Hour == 1 && DateTime.UtcNow.Minute == 1)
                        {
                            await RunIndicator(IntervalType.OneDay, stoppingToken);
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
}