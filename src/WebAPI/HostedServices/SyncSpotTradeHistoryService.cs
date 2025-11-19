using Auth.Infrastructure;
using Cex.Application.BnbSpotOrder.Commands.SyncSpotOrders;
using Cex.Application.KuCoin.Commands.SyncTradeHistory;
using Cex.Infrastructure;
using Lib.Notifications;
using MediatR;
using Serilog;

namespace WebAPI.HostedServices
{
    public class SyncSpotTradeHistoryService(IConfiguration configuration) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddCexInfrastructureServices(configuration);
            services.AddTelegramNotifier(configuration);
            services.AddTransient(p => new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger());
            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
                logger.Information("Start {serviceName} started at {startAt}", GetType().Name, DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        await mediator.Send(new SyncTradeHistoryCommand(new DateTime(2025, 9, 20)), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.Information("Exception {serviceName} - {message}", GetType().Name, ex.Message);
                    }

                    await Task.Delay(5 * 60 * 1000, stoppingToken);
                }
            }, stoppingToken);
        }
    }
}