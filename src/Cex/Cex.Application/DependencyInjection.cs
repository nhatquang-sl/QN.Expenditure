using System.Reflection;
using FluentValidation;
using Lib.Application;
using Lib.Application.Behaviors;
using Lib.ExternalServices;
using MediatR;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cex.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCexApplicationServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddLibApplicationServices(configuration);

            var environment = configuration.GetValue<string>("Environment") ?? "";
            if (!environment.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                services.AddLibExternalServices(configuration);
            }

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                cfg.NotificationPublisher = new TaskWhenAllPublisher();
            });
            services.AddAutoMapper(typeof(MappingProfile));

            return services;
        }
    }
}