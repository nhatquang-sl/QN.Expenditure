using Application.Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Application.UnitTests
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly IServiceScope _scope;
        public DependencyInjectionFixture()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddApplicationServices();
            serviceCollection.AddScoped<ILogTrace>(x => new Mock<ILogTrace>().Object);
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
