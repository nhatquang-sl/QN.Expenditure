using Lib.ExternalServices.Bnb;
using Lib.ExternalServices.Cex;
using Lib.ExternalServices.Email;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.Telegram;
using Mailjet.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Lib.ExternalServices
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLibExternalServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services
                .AddRefitClient<ICexService>()
                .ConfigureHttpClient(c =>
                {
                    c.BaseAddress = new Uri(configuration.GetValue("CexConfig:ApiEndpoint", "") ?? "");
                });

            services.Configure<TelegramServiceConfig>(configuration.GetSection("TelegramServiceConfig"));
            services
                .AddRefitClient<ITelegramService>()
                .ConfigureHttpClient(c => { c.BaseAddress = new Uri("https://api.telegram.org"); });

            services
                .AddRefitClient<IBnbService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.binance.com"));

            services.Configure<KuCoinConfig>(configuration.GetSection("KuCoinConfig"));
            services
                .AddRefitClient<IKuCoinService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.kucoin.com"));

            services.AddEmailServices(configuration);
            return services;
        }

        private static void AddEmailServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<EmailConfig>(configuration.GetSection("Email"));
            services.AddHttpClient<IMailjetClient, MailjetClient>((serviceProvider, httpClient) =>
            {
                var config = serviceProvider.GetRequiredService<EmailConfig>();
                // var apiKey = configuration.GetValue<string>("Email:ApiKeyPublic");
                // var apiSecret = configuration.GetValue<string>("Email:ApiKeyPrivate");
                httpClient.UseBasicAuthentication(config.ApiKeyPublic, config.ApiKeyPrivate);
            });
            services.AddTransient<IEmailService, EmailService>();
        }
    }
}