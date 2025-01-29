using Lib.Application.Abstractions;
using Lib.ExternalServices.KuCoin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cex.Infrastructure.IntegrationTests
{
    public class DependencyInjectionFixture : IDisposable
    {
        protected readonly ServiceCollection ServiceCollection;
        private IServiceScope? _scope;

        protected DependencyInjectionFixture()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            ServiceCollection = [];
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
    }
}