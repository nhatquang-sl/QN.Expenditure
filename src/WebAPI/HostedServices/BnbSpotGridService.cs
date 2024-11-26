
using Application.BnbSpotGrid.Commands.TradeSpotGrid;
using Infrastructure;
using MediatR;
using Serilog;

namespace WebAPI.HostedServices
{
    public class BnbSpotGridService(IConfiguration configuration) : BackgroundService
    {
        private readonly IConfiguration _configuration = configuration;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var services = new ServiceCollection();
            services.AddSingleton(_configuration);
            services.AddInfrastructureServices(_configuration);
            services.AddTransient(p => new LoggerConfiguration().ReadFrom.Configuration(_configuration).CreateLogger());
            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                var logger = new LoggerConfiguration().ReadFrom.Configuration(_configuration).CreateLogger();
                logger.Information("Start {serviceName} started at {startAt}", GetType().Name, DateTime.UtcNow);
                while (true)
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        await mediator.Send(new TradeSpotGridCommand());
                    }
                    catch (Exception ex)
                    {
                        logger.Information("Exception {serviceName} - {message}", this.GetType().Name, ex.Message);
                    }

                    await Task.Delay(5 * 60 * 1000);
                }
            });
        }
    }
}
