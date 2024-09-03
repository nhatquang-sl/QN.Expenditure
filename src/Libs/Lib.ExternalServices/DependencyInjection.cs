using Lib.ExternalServices.Cex;
using Lib.ExternalServices.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Lib.ExternalServices
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLibExternalServices(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddRefitClient<ICexService>()
                .ConfigureHttpClient((c) =>
                {
                    c.BaseAddress = new Uri(configuration.GetValue("CexConfig:ApiEndpoint", "") ?? "");
                });

            services
                .AddRefitClient<ITelegramService>()
                .ConfigureHttpClient((c) =>
                {
                    c.BaseAddress = new Uri(configuration.GetValue("TelegramServiceConfig:ApiEndpoint", "") ?? "");
                });

            services.Configure<TelegramServiceConfig>(configuration.GetSection("TelegramServiceConfig"));

            return services;
        }
    }
}
