using Auth.Application.Account.DTOs;
using Lib.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure.IntegrationTests
{
    public class DependencyInjectionFixture
    {
        private readonly IServiceScope _scope;

        protected DependencyInjectionFixture()
        {
            var serviceCollection = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Environment", "test" }
                })
                .Build();
            serviceCollection.AddAuthInfrastructureServices(configuration);
            serviceCollection.AddSingleton<ICurrentUser>(x => new UserProfileDto
            {
                Id = Guid.NewGuid().ToString(),
                Email = "email@gmail.com",
                FirstName = "First",
                LastName = "Last",
                EmailConfirmed = true
            });
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _scope = serviceProvider.CreateScope();
        }

        protected T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}