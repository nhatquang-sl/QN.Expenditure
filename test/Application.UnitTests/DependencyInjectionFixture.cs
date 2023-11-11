using Microsoft.Extensions.DependencyInjection;

namespace Application.UnitTests
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly IServiceScope _scope;
        public DependencyInjectionFixture()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddApplicationServices();
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
