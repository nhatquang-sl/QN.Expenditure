using System.Reflection;
using FluentValidation;
using Lib.Application;
using Lib.Application.Behaviors;
using MediatR;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAuthApplicationServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddLibApplicationServices(configuration);

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                cfg.NotificationPublisher = new TaskWhenAllPublisher();
            });

            return services;
        }
    }
}