using Auth.Infrastructure;
using Lib.ExternalServices.KuCoin;
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
            services.AddSingleton(configuration);
            services.AddInfrastructureServices(configuration);
            services.AddTransient(p => new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger());
            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    var apiKey = kuCoinConfig.Value.ApiKey;
                    var apiSecret = kuCoinConfig.Value.ApiSecret;
                    var apiPassphrase = kuCoinConfig.Value.ApiPassphrase;

                    var order = new OrderRequest
                    {
                        ClientOid = Guid.NewGuid().ToString(),
                        Side = "buy",
                        Symbol = "BTC-USDT",
                        Type = "limit",
                        Price = "3000",
                        Size = "0.002"
                    };

                    var placeOrderRes = await kuCoinService.PlaceOrder(
                        order,
                        apiKey,
                        apiSecret,
                        apiPassphrase
                    );
                }
                catch (Exception ex)
                {
                }

                var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
                logger.Information("Start {serviceName} started at {startAt}", GetType().Name, DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        // await mediator.Send(new TradeSpotGridCommand());
                    }
                    catch (Exception ex)
                    {
                        logger.Information("Exception {serviceName} - {message}", GetType().Name, ex.Message);
                    }

                    await Task.Delay(5 * 60 * 1000);
                }
            });
        }
    }
}