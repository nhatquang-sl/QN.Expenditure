using FluentValidation;
using Lib.Application.Behaviors;
using Lib.Application.Logging;
using MediatR;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Reflection;

namespace Lib.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddLibApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient(p => new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger());
            services.AddScoped<ILogTrace, LogTrace>();
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
