using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.ExternalServices
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLibExternalServices(this IServiceCollection services, IConfiguration configuration)
        {

            return services;
        }
    }
}
