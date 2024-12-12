using Auth.Infrastructure;
using Cex.Application.BnbSpotOrder.Commands.Arbitrage;
using MediatR;
using Serilog;

namespace WebAPI.HostedServices
{
    public class ArbitrageService(IConfiguration configuration) : BackgroundService
    {
        private readonly IConfiguration _configuration = configuration;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var services = new ServiceCollection();
            services.AddInfrastructureServices(_configuration);
            services.AddTransient(p => new LoggerConfiguration().ReadFrom.Configuration(_configuration).CreateLogger());
            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                var logger = new LoggerConfiguration().ReadFrom.Configuration(_configuration).CreateLogger();
                logger.Information("Start ArbitrageService started at {startAt}", DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new ArbitrageCommand());
                    }
                    catch (Exception ex)
                    {
                        var exception = ex;
                        do
                        {
                            logger.Error(exception, "Error ArbitrageService");
                            exception = exception.InnerException;
                        } while (exception != null);
                    }

                    await Task.Delay(60 * 1000);
                }
            }, stoppingToken);
        }
    }
}