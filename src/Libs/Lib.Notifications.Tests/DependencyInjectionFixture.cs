using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Lib.Notifications.Tests
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly ServiceCollection _serviceCollection;
        private IServiceScope? _scope;

        protected DependencyInjectionFixture()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(@"D:\QN.Expenditure\src\WebAPI\QN.Expenditure.Credentials\appsettings.json")
                .Build();

            var mockConfiguration = new Mock<IConfiguration>();
            var mockSection = new Mock<IConfigurationSection>();

            // Mock the Value property of the IConfigurationSection
            mockSection.Setup(x => x.Value)
                .Returns(configuration.GetValue<string>("Notifier:PathUrl") ?? "");

            // Mock the GetSection method of IConfiguration to return the mocked section
            mockConfiguration
                .Setup(x => x.GetSection("Notifier:PathUrl"))
                .Returns(mockSection.Object);

            _serviceCollection = [];
            _serviceCollection.AddSingleton(mockConfiguration.Object);
            _serviceCollection.AddTelegramNotifier(mockConfiguration.Object);
        }

        public void Dispose()
        {
            _scope?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected T GetService<T>() where T : notnull
        {
            if (_scope != null)
            {
                return _scope.ServiceProvider.GetRequiredService<T>();
            }

            var serviceProvider = _serviceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();

            return _scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}