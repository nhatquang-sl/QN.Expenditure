using Lib.Application.Abstractions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.EventBus;

public static class DependencyInjection
{
    public static IServiceCollection AddLibEventBusServices(this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.Configure<RabbitMqConfig>(configuration.GetSection("RabbitMq"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RabbitMqConfig>>().Value);
        services.AddMassTransit(busConfig =>
        {
            busConfig.SetKebabCaseEndpointNameFormatter();

            configureBus?.Invoke(busConfig);

            busConfig.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConfig = context.GetRequiredService<RabbitMqConfig>();
                cfg.Host(new Uri(rabbitMqConfig.Host), h =>
                {
                    h.Username(rabbitMqConfig.Username);
                    h.Password(rabbitMqConfig.Password);
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddTransient<IEventBus, RabbitMqEventBus>();

        return services;
    }
}