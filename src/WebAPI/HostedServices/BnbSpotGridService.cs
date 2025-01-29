using Cex.Application.Grid.Commands.TradeSpotGrid;
using Cex.Infrastructure;
using Lib.ExternalServices.KuCoin;
using Lib.Notifications;
using MediatR;
using Microsoft.Extensions.Options;
using Serilog;

namespace WebAPI.HostedServices
{
    public class BnbSpotGridService(
        IConfiguration configuration,
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig) : BackgroundService
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
                logger.Information("Start {serviceName} started at {startAt}", GetType().Name, DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                        await mediator.Send(new TradeSpotGridCommand(), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.Information("Exception {serviceName} - {message}", GetType().Name, ex.Message);
                    }

                    await Task.Delay(1 * 60 * 1000, stoppingToken);
                }
            }, stoppingToken);
        }
    }
}