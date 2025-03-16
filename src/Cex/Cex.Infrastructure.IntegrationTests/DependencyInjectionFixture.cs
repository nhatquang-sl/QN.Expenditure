using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Application.Grid.DTOs;
using Cex.Domain.Entities;
using Cex.Infrastructure.Data;
using Lib.Application.Abstractions;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cex.Infrastructure.IntegrationTests
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        protected readonly CreateSpotGridCommand CreateCommand = new("BTCUSDT", 60, 70
            , 50, 5, SpotGridMode.ARITHMETIC, 100, 110, 30);

        protected readonly Mock<IKuCoinService> KuCoinServiceMock = new();

        protected readonly ServiceCollection ServiceCollection;

        protected readonly SpotGridDto SpotGridCreated;

        private IServiceScope? _scope;


        protected DependencyInjectionFixture()
        {
            // Create an in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open(); // Keeps the in-memory database alive for the test duration

            var options = new DbContextOptionsBuilder<CexDbContext>()
                .UseSqlite(_connection) // Use the same in-memory database
                .Options;

            var context = new CexDbContext(options);
            context.Database.EnsureCreated(); // Ensures the schema is created fresh
            context.Dispose();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            ServiceCollection = [];
            ServiceCollection.AddDbContext<CexDbContext>(op =>
            {
                op.UseSqlite(_connection)
                    .LogTo(Console.WriteLine, LogLevel.Information) // Logs SQL queries to console;
                    .EnableSensitiveDataLogging(); // Shows parameter values in SQL logs (for debugging)
            });
            ServiceCollection.AddCexInfrastructureServices(configuration);
            ServiceCollection.AddSingleton(new Mock<KuCoinConfig>().Object);
            var currentUserMock = new Mock<ICurrentUser>();
            currentUserMock.SetupGet(user => user.Id).Returns(Guid.NewGuid().ToString());
            ServiceCollection.AddSingleton(currentUserMock.Object);
            ServiceCollection.AddSingleton(new Mock<INotifier>().Object);
            ServiceCollection.AddSingleton(KuCoinServiceMock.Object);

            var serviceProvider = ServiceCollection.BuildServiceProvider();
            var scope = serviceProvider.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            SpotGridCreated = sender.Send(CreateCommand).Result;
            scope.Dispose();
        }

        public void Dispose()
        {
            _scope?.Dispose();
            _connection.Close(); // Destroys the in-memory database
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }

        protected T GetService<T>() where T : notnull
        {
            if (_scope != null)
            {
                return _scope.ServiceProvider.GetRequiredService<T>();
            }

            var serviceProvider = ServiceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();

            return _scope.ServiceProvider.GetRequiredService<T>();
        }

        public static IEnumerable<object[]> GetNormalStepsBaseOnInvestment()
        {
            yield return new object[]
            {
                150m,
                new decimal[] { 60, 62, 64, 66, 68 },
                new decimal[] { 62, 64, 66, 68, 70 },
                new[] { 0.375m, 0.3629m, 0.3515m, 0.3409m, 0.3308m }
            };
            yield return new object[]
            {
                110m,
                new decimal[] { 60, 62, 64, 66, 68 },
                new decimal[] { 62, 64, 66, 68, 70 },
                new[] { 0.275m, 0.2661m, 0.2578m, 0.25m, 0.2426m }
            };
        }

        public static IEnumerable<object[]> GetNormalStepsBaseOnNumOfGrids()
        {
            yield return new object[]
            {
                2,
                new decimal[] { 60, 65 },
                new decimal[] { 65, 70 },
                new[] { 0.625m, 0.5769m }
            };
            yield return new object[]
            {
                3,
                new[] { 60, 63.3333m, 66.6666m },
                new[] { 63.3333m, 66.6666m, 70 },
                new[] { 0.4166m, 0.3947m, 0.375m }
            };
            yield return new object[]
            {
                4,
                new[] { 60, 62.5m, 65, 67.5m },
                new[] { 62.5m, 65, 67.5m, 70 },
                new[] { 0.3125m, 0.3m, 0.2884m, 0.2777m }
            };
        }

        public static IEnumerable<object[]> GetGridStepTestData()
        {
            yield return new object[]
            {
                50m, 80m, new decimal[] { 50, 56, 62, 68, 74 },
                new decimal[] { 56, 62, 68, 74, 80 },
                new[] { 0.3m, 0.2678m, 0.2419m, 0.2205m, 0.2027m }
            };
            yield return new object[]
            {
                55m, 70m, new decimal[] { 55, 58, 61, 64, 67 },
                new decimal[] { 58, 61, 64, 67, 70 },
                new[] { 0.2727m, 0.2586m, 0.2459m, 0.2343m, 0.2238m }
            };
        }
    }
}