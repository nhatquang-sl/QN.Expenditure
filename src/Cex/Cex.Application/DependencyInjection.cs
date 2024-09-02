using Lib.Application;
using Lib.ExternalServices.Cex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using System.Reflection;

namespace Cex.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCexApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLibApplicationServices(configuration);

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });

            services
                .AddRefitClient<ICexService>()
                .ConfigureHttpClient((c) =>
                {
                    c.BaseAddress = new Uri(configuration.GetValue("CexConfig:ApiEndpoint", "") ?? "");
                });

            services.AddAutoMapper(typeof(MappingProfile));

            return services;
        }
    }
}
