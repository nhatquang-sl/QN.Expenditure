using Application.Auth.Commands.Register;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infrastructure.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<ApplicationUser> _userManager;


        public IdentityService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<string> CreateUserAsync(RegisterCommand request)
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var duplicateErr = result.Errors.FirstOrDefault(x => x.Code == "DuplicateUserName");
                if (duplicateErr != null)
                {
                    var regex = new Regex(Regex.Escape("Username"));
                    throw new ConflictException(new { email = regex.Replace(duplicateErr.Description, "Email", 1) });
                }

                throw new Exception($"UnhandledException: {GetType().Name}");
            }
            Console.WriteLine(result.Succeeded);
            Console.WriteLine(JsonSerializer.Serialize(result.Errors));
            return user.Id ?? "";
        }
    }
}
