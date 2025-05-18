using Auth.Application.Common.Abstractions;
using Lib.Application.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Auth.Application.UnitTest
{
    public class DependencyInjectionFixture : IDisposable
    {
        private readonly IServiceScope _scope;

        protected DependencyInjectionFixture()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddAuthApplicationServices(new Mock<IConfiguration>().Object);
            serviceCollection.AddScoped(p => new Mock<ILogTrace>().Object);
            serviceCollection.AddTransient(p => new Mock<IIdentityService>().Object);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        protected T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}