using Application.Common.Behaviors;
using Application.Common.ExServices.Bnb;
using FluentValidation;
using MediatR;
using MediatR.NotificationPublishers;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using System.Reflection;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                cfg.NotificationPublisher = new TaskWhenAllPublisher();
            });


            services
                .AddRefitClient<IBndService>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.binance.com"));

            return services;
        }
    }
}