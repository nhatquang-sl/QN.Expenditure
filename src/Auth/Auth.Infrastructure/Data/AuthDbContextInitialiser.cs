using Auth.Application.Account.Commands.Register;
using Auth.Infrastructure.Identity;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Data
{
    public static class InitializerExtensions
    {
        public static async Task InitializeDatabaseAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var initializer = scope.ServiceProvider.GetRequiredService<AuthDbContextInitializer>();

            await initializer.InitializeAsync();

            await initializer.SeedAsync();
        }
    }

    public class AuthDbContextInitializer(
        ILogger<AuthDbContextInitializer> logger,
        AuthDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IMapper mapper,
        IConfiguration configuration)
    {
        public async Task InitializeAsync()
        {
            try
            {
                var conn = context.Database.GetConnectionString();
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        public async Task SeedAsync()
        {
            try
            {
                await TrySeedAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        public async Task TrySeedAsync()
        {
            // Default roles
            var administratorRole = new IdentityRole("Administrator");

            if (roleManager.Roles.All(r => r.Name != administratorRole.Name))
            {
                await roleManager.CreateAsync(administratorRole);
            }

            // Default users
            var administrator = configuration.GetSection("Administrator").Get<RegisterCommand>();
            if (administrator != null && userManager.Users.All(u => u.UserName != administrator.Email))
            {
                var user = mapper.Map<ApplicationUser>(administrator);
                user.EmailConfirmed = true;
                await userManager.CreateAsync(user, administrator.Password);
                if (!string.IsNullOrWhiteSpace(administratorRole.Name))
                {
                    await userManager.AddToRolesAsync(user, new[] { administratorRole.Name });
                }
            }

            var defaultUser = configuration.GetSection("DefaultUser").Get<RegisterCommand>();
            if (defaultUser != null && userManager.Users.All(u => u.UserName != defaultUser.Email))
            {
                var user = mapper.Map<ApplicationUser>(defaultUser);
                user.EmailConfirmed = true;
                var result = await userManager.CreateAsync(user, defaultUser.Password);

                // var bnbSetting = _configuration.GetSection("BnbSetting").Get<BnbSettingDto>();
                // if (bnbSetting != null)
                // {
                //     _context.BnbSettings.Add(new Domain.Entities.BnbSetting
                //     {
                //         UserId = user.Id,
                //         ApiKey = bnbSetting.ApiKey,
                //         SecretKey = bnbSetting.SecretKey
                //     });
                //     _context.SaveChanges();
                // }
            }
        }
    }
}