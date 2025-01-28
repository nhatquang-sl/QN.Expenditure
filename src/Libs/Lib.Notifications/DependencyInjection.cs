using Lib.Application.Abstractions;
using Lib.ExternalServices;
using Lib.ExternalServices.Telegram;
using Lib.Notifications.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Lib.Notifications
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddTelegramNotifier(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddTransient<HttpDelegatingHandler>();
            services
                .AddRefitClient<ITelegramService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.telegram.org"))
                .AddHttpMessageHandler<HttpDelegatingHandler>();
            ;

            services.AddScoped<INotifier, TelegramNotifier>();

            return services;
        }
    }
}