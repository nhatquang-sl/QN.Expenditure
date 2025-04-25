using Lib.Application.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLibApplicationServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddScoped<ILogTrace, LogTrace>();

            return services;
        }
    }
}