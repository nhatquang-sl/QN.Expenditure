using System.Text;
using Cex.Application.Grid.Commands.TradeSpotGrid;
using Cex.Application.Grid.Queries.GetDailyStatistics;
using Cex.Infrastructure;
using Lib.Application.Abstractions;
using Lib.Notifications;
using MediatR;
using Serilog;

namespace WebAPI.HostedServices
{
    public class SpotGridService(
        IConfiguration configuration) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var startedAt = DateTime.UtcNow;
            var services = new ServiceCollection();
            services.AddCexInfrastructureServices(configuration);
            services.AddTelegramNotifier(configuration);
            var serviceProvider = services.BuildServiceProvider();

            await Task.Factory.StartNew(async () =>
            {
                var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
                logger.Information("Start {serviceName} started at {startAt}", GetType().Name, startedAt);
                while (true)
                {
                    using var scope = serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                    var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
                    try
                    {
                        await mediator.Send(new TradeSpotGridCommand(), stoppingToken);

                        await SendDailyStatistics(mediator, notifier, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Exception {serviceName} - {message}: {stackTrace}", GetType().Name, ex.Message,
                            ex.StackTrace);
                        await notifier.NotifyError("SpotGridService", ex, stoppingToken);
                    }

                    await Task.Delay(1 * 60 * 1000, stoppingToken);
                }
            }, stoppingToken);
        }

        private static async Task SendDailyStatistics(ISender mediator,
            INotifier notifier, CancellationToken stoppingToken)
        {
            var currentDate = DateTime.UtcNow;
            if (currentDate.Minute != 5)
            {
                return;
            }

            var yesterday = currentDate.AddDays(-1);
            var profitByUsers = await mediator.Send(new GetDailyStatisticsQuery(yesterday), stoppingToken);
            var message = new StringBuilder();
            foreach (var profitByUser in profitByUsers)
            {
                message.AppendLine($"User {profitByUser.UserId} profit: {profitByUser.Profit}");
            }

            if (profitByUsers.Count == 0)
            {
                message.AppendLine("No trades were made.");
            }

            await notifier.NotifyInfo($"Daily statistics for {yesterday.ToShortDateString()}",
                message.ToString(), stoppingToken);
        }
    }
}