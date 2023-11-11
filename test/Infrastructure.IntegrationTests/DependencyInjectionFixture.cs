using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.IntegrationTests
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly IServiceScope _scope;
        public DependencyInjectionFixture()
        {
            var config = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddInfrastructureServices(config);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();
        }

        public T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
