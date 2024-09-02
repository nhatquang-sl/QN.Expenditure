using Cex.Application;
using Cex.Application.Common.Abstractions;
using Cex.Application.Common.Configs;
using Cex.Infrastructure.Data;
using Cex.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cex.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddCexInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(configuration);
            services.AddCexApplicationServices(configuration);
            services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

            var environment = configuration.GetValue<string>("Environment");
            if (environment?.ToLower() == "test")
            {
                services.AddDbContext<CexDbContext>((options) =>
                {
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString());
                });
            }
            else
            {
                var cexConnectionString = configuration.GetConnectionString("CexConnection");

                services.AddDbContext<CexDbContext>((options) =>
                {
                    options.UseSqlServer(cexConnectionString);
                });
            }

            services.AddScoped<ICexDbContext>(provider => provider.GetRequiredService<CexDbContext>());

            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //            .AddJwtBearer();
            //services.ConfigureOptions<JwtBearerSetup>();

            //services.Configure<EmailConfig>(configuration.GetSection("Email"));
            //services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
            // need Microsoft.Extensions.Options.ConfigurationExtensions nuget package
            services.Configure<CexConfig>(configuration.GetSection("CexConfig"));

            //services.AddTransient<IIdentityService, IdentityService>();
            //services.AddTransient<IEmailService, EmailService>();
            //services.AddTransient<IJwtProvider, JwtProvider>();

            //services.AddScoped<ILogTrace, LogTrace>();

            //services.AddScoped<ApplicationDbContextInitializer>();

            //services.AddHttpClient<IMailjetClient, MailjetClient>(client =>
            //{
            //    var apiKey = configuration.GetValue<string>("Email:ApiKeyPublic");
            //    var apiSecret = configuration.GetValue<string>("Email:ApiKeyPrivate");
            //    client.UseBasicAuthentication(apiKey, apiSecret);
            //});

            //services.AddAutoMapper(typeof(MappingProfile));

            return services;
        }
    }
}
