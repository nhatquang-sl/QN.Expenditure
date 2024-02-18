using Application;
using Application.Common.Abstractions;
using Application.Common.Configs;
using Application.Common.Logging;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Services;
using Mailjet.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddApplicationServices();
            var environment = configuration.GetValue<string>("Environment");
            if (environment?.ToLower() == "test")
            {
                services.AddDbContext<ApplicationDbContext>((options) =>
                {
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString());
                });
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                Console.WriteLine(connectionString);
                services.AddDbContext<ApplicationDbContext>((options) =>
                {
                    options.UseSqlServer(connectionString);
                });
            }

            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
            services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                        .AddRoles<IdentityRole>()
                        .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer();
            services.ConfigureOptions<JwtBearerSetup>();

            services.Configure<EmailConfig>(configuration.GetSection("Email"));
            services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
            services.Configure<ApplicationConfig>(configuration.GetSection("Application"));

            services.AddTransient<IIdentityService, IdentityService>();
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<IJwtProvider, JwtProvider>();

            services.AddScoped<ILogTrace, LogTrace>();

            services.AddScoped<ApplicationDbContextInitializer>();

            services.AddHttpClient<IMailjetClient, MailjetClient>(client =>
            {
                var apiKey = configuration.GetValue<string>("Email:ApiKeyPublic");
                var apiSecret = configuration.GetValue<string>("Email:ApiKeyPrivate");
                client.UseBasicAuthentication(apiKey, apiSecret);
            });

            services.AddAutoMapper(typeof(MappingProfile));

            return services;
        }
    }
}