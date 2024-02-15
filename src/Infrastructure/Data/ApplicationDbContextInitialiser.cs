using Application.Auth.Commands.Register;
using Application.BnbSetting.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data
{
    public static class InitializerExtensions
    {
        public static async Task InitializeDatabaseAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();

            var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitializer>();

            await initializer.InitializeAsync();

            await initializer.SeedAsync();
        }
    }

    public class ApplicationDbContextInitializer
    {
        private readonly ILogger<ApplicationDbContextInitializer> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IApplicationDbContext _applicationDbContext;

        public ApplicationDbContextInitializer(ILogger<ApplicationDbContextInitializer> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager
            , IMapper mapper, IConfiguration configuration, IApplicationDbContext applicationDbContext)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
            _configuration = configuration;
            _applicationDbContext = applicationDbContext;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var conn = _context.Database.GetConnectionString();
                await _context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
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
                _logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        public async Task TrySeedAsync()
        {
            // Default roles
            var administratorRole = new IdentityRole("Administrator");

            if (_roleManager.Roles.All(r => r.Name != administratorRole.Name))
            {
                await _roleManager.CreateAsync(administratorRole);
            }

            // Default users
            var administrator = new ApplicationUser { UserName = "administrator@localhost", Email = "administrator@localhost", FirstName = "First", LastName = "Last" };
            administrator.EmailConfirmed = true;
            if (_userManager.Users.All(u => u.UserName != administrator.UserName))
            {
                await _userManager.CreateAsync(administrator, "Administrator1!");
                if (!string.IsNullOrWhiteSpace(administratorRole.Name))
                {
                    await _userManager.AddToRolesAsync(administrator, new[] { administratorRole.Name });
                }
            }

            var defaultUser = _configuration.GetSection("DefaultUser").Get<RegisterCommand>();
            if (defaultUser != null && _userManager.Users.All(u => u.UserName != defaultUser.Email))
            {
                var user = _mapper.Map<ApplicationUser>(defaultUser);
                user.EmailConfirmed = true;
                var result = await _userManager.CreateAsync(user, defaultUser.Password);

                var bnbSetting = _configuration.GetSection("BnbSetting").Get<BnbSettingDto>();
                if (bnbSetting != null)
                {
                    _context.BnbSettings.Add(new Domain.Entities.BnbSetting
                    {
                        UserId = user.Id,
                        ApiKey = bnbSetting.ApiKey,
                        SecretKey = bnbSetting.SecretKey,
                    });
                    _context.SaveChanges();
                }
            }

        }
    }
}
