using Application.BnbSpotOrder.Commands.SyncSpotOrders;
using Infrastructure;
using MediatR;

namespace WebAPI.HostedServices
{
    public class SyncSpotOrdersService(IConfiguration configuration) : BackgroundService
    {
        private readonly IConfiguration _configuration = configuration;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddInfrastructureServices(_configuration);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        using (var scope = serviceProvider.CreateScope())
                        {
                            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                            await mediator.Send(new SyncSpotOrdersCommand());
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                    await Task.Delay(60 * 1000);
                }
            });
        }

    }
}
