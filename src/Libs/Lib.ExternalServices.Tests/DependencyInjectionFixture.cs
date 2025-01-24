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
                .AddJsonFile("D:\\QN.Expenditure\\src\\WebAPI\\QN.Expenditure.Credentials\\appsettings.json")
                .Build();

            var serviceCollection = new ServiceCollection();

            serviceCollection.Configure<KuCoinConfig>(configuration.GetSection("KuCoinConfig"));
            serviceCollection
                .AddRefitClient<IKuCoinService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.kucoin.com"));

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