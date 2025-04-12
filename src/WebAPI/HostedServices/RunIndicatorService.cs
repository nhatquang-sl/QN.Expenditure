using Cex.Application.Indicator.Commands;
using Cex.Infrastructure;
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
            services.AddTransient(p => new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger());
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
                        await mediator.Send(new RunIndicatorCommand(), stoppingToken);
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