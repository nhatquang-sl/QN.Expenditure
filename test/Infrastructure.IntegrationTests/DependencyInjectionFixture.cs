using Application.Common.Abstractions;
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
            serviceCollection.AddSingleton<ICurrentUser>(x => new CurrentUser
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

        public T GetService<T>() where T : notnull
        {
            return _scope.ServiceProvider.GetRequiredService<T>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }

    public class CurrentUser : ICurrentUser
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool EmailConfirmed { get; set; }
    }
}
