using Lib.ExternalServices.KuCoin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Lib.ExternalServices.Tests
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly IServiceScope _scope;

        public DependencyInjectionFixture()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(
                    "/Users/quang/Workspace/QN.Expenditure/src/WebAPI/QN.Expenditure.Credentials/appsettings.json")
                .Build();
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_DISABLEIPV6", "true");
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<HttpDelegatingHandler>();

            serviceCollection.Configure<KuCoinConfig>(configuration.GetSection("KuCoinConfig"));
            serviceCollection
                .AddRefitClient<IKuCoinService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.kucoin.com"))
                .AddHttpMessageHandler<HttpDelegatingHandler>();
            serviceCollection
                .AddRefitClient<IFtKuCoinService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api-futures.kucoin.com"))
                .AddHttpMessageHandler<HttpDelegatingHandler>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();
        }

        public void Dispose()
        {
            _scope.Dispose();
            GC.SuppressFinalize(this);
        }

        public T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}