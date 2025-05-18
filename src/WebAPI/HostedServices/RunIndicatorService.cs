using Cex.Application.Indicator.Commands;
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
                // using var scope = serviceScopeFactory.CreateScope();
                // var logger = scope.ServiceProvider.GetRequiredService<Logger>();
                // logger.Information("Start {serviceName}", GetType().Name);
                // logger.Information("Start RunIndicatorService started at {startAt}", DateTime.UtcNow);
                logger.LogInformation("Started at {startAt}", DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        // await mediator.Send(new StatisticIndicatorCommand(), stoppingToken);
                        // await RunIndicator(IntervalType.FiveMinutes, stoppingToken);
                        // await RunIndicator(IntervalType.FifteenMinutes, stoppingToken);

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
                        var exception = ex;
                        do
                        {
                            // logger.Error(exception, "Error RunIndicatorService");
                            exception = exception.InnerException;
                        } while (exception != null);
                    }

                    await Task.Delay(60 * 1000, stoppingToken);
                }
            }, stoppingToken);
        }
    }
}