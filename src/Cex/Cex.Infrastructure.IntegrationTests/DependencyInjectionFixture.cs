using Cex.Infrastructure.Data;
using Lib.Application.Abstractions;
using Lib.ExternalServices.KuCoin;
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
        protected readonly CexDbContext Context;
        protected readonly ServiceCollection ServiceCollection;
        private IServiceScope? _scope;


        protected DependencyInjectionFixture()
        {
            // Create an in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open(); // Keeps the in-memory database alive for the test duration

            var options = new DbContextOptionsBuilder<CexDbContext>()
                .UseSqlite(_connection) // Use the same in-memory database
                .Options;

            Context = new CexDbContext(options);
            Context.Database.EnsureCreated(); // Ensures the schema is created fresh
            Context.Dispose();

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

        public static IEnumerable<object[]> GetGridStepTestData()
        {
            yield return new object[]
            {
                60m, 70m, new decimal[] { 60, 62, 64, 66, 68 },
                new decimal[] { 62, 64, 66, 68, 70 },
                new[] { 0.25m, 0.2419m, 0.2343m, 0.2272m, 0.2205m }
            };
            yield return new object[]
            {
                55m, 70m, new decimal[] { 55, 58, 61, 64, 67 },
                new decimal[] { 58, 61, 64, 67, 70 },
                new[] { 0.2727m, 0.2586m, 0.2459m, 0.2343m, 0.2238m }
            };
        }

        public static IEnumerable<object[]> GetGrid2StepAwaitingSellData()
        {
            yield return new object[]
            {
                60m, 70m, new decimal[] { 60, 62, 64, 66, 68 },
                new decimal[] { 62, 64, 66, 68, 70 },
                new[] { 0.175m, 0.1693m, 0.164m, 0.1591m, 0.1544m }
            };
            yield return new object[]
            {
                55m, 70m, new decimal[] { 55, 58, 61, 64, 67 },
                new decimal[] { 58, 61, 64, 67, 70 },
                new[] { 0.1909m, 0.181m, 0.1721m, 0.164m, 0.1567m }
            };
        }
    }
}