using Lib.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            return services;
        }
    }
}
