using Auth.Application;
using Auth.Application.Common.Abstractions;
using Auth.Infrastructure.Data;
using Auth.Infrastructure.Identity;
using Lib.Application.Configs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton(configuration);
            services.AddAuthApplicationServices(configuration);
            var environment = configuration.GetValue<string>("Environment");
            if (environment?.ToLower() == "test")
            {
                services.AddDbContext<AuthDbContext>(options =>
                {
                    options.UseInMemoryDatabase(Guid.NewGuid().ToString());
                });
            }
            else
            {
                var connectionString = configuration.GetConnectionString("AuthConnection");
                services.AddDbContext<AuthDbContext>(options =>
                {
                    options.LogTo(Console.WriteLine);
                    options.UseSqlServer(connectionString);
                });
            }

            services.AddScoped<IAuthDbContext>(provider => provider.GetRequiredService<AuthDbContext>());
            services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AuthDbContext>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();
            services.ConfigureOptions<JwtBearerSetup>();

            services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
            services.Configure<ApplicationConfig>(configuration.GetSection("Application"));

            services.AddTransient<IIdentityService, IdentityService>();
            services.AddTransient<IJwtProvider, JwtProvider>();
            services.AddScoped<AuthDbContextInitializer>();
            services.AddAutoMapper(typeof(MappingProfile));

            return services;
        }
    }
}