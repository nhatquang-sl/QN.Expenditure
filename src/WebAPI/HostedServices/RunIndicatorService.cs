using Cex.Application.Indicator.Commands;
using Cex.Infrastructure;
using Lib.Notifications;
using MediatR;
using Serilog;

namespace WebAPI.HostedServices
{
    public class RunIndicatorService(IConfiguration configuration) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var services = new ServiceCollection();
            services.AddCexInfrastructureServices(configuration);
            services.AddTelegramNotifier(configuration);

            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
                logger.Information("Start RunIndicatorService started at {startAt}", DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        // await mediator.Send(new StatisticIndicatorCommand(), stoppingToken);
                        if (DateTime.UtcNow.Minute % 5 == 1)
                        {
                            await mediator.Send(new RunIndicatorCommand(IntervalType.FiveMinutes), stoppingToken);
                        }

                        if (DateTime.UtcNow.Minute % 15 == 1)
                        {
                            await mediator.Send(new RunIndicatorCommand(IntervalType.FifteenMinutes), stoppingToken);
                        }

                        if (DateTime.UtcNow.Minute % 30 == 1)
                        {
                            await mediator.Send(new RunIndicatorCommand(IntervalType.ThirtyMinutes), stoppingToken);
                        }

                        if (DateTime.UtcNow.Minute == 1)
                        {
                            await mediator.Send(new RunIndicatorCommand(IntervalType.OneHour), stoppingToken);
                        }

                        if (DateTime.UtcNow.Hour % 4 == 1 && DateTime.UtcNow.Minute == 1)
                        {
                            await mediator.Send(new RunIndicatorCommand(IntervalType.FourHours), stoppingToken);
                        }

                        if (DateTime.UtcNow.Hour == 1 && DateTime.UtcNow.Minute == 1)
                        {
                            await mediator.Send(new RunIndicatorCommand(IntervalType.OneDay), stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        var exception = ex;
                        do
                        {
                            logger.Error(exception, "Error RunIndicatorService");
                            exception = exception.InnerException;
                        } while (exception != null);
                    }

                    await Task.Delay(60 * 1000, stoppingToken);
                }
            }, stoppingToken);
        }
    }
}