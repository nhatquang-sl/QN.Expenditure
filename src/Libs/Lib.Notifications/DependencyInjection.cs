using Lib.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.Notifications
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddTelegramNotifier(this IServiceCollection services,
            IConfiguration configuration)
        {
            var environment = configuration.GetValue<string>("Environment");

            services.AddHttpClient<INotifier, TelegramNotifier>((provider, client) =>
            {
                client.BaseAddress = new Uri("https://api.telegram.org/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            services.AddTransient<INotifier>(provider =>
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                return new TelegramNotifier(httpClient, configuration.GetValue<string>("Notifier:PathUrl") ?? "");
            });

            return services;

            return services;
        }
    }
}