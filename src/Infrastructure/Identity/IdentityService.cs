using Application.Auth.Commands.Register;
using Application.Common.Abstractions;
using Application.Common.Configs;
using Application.Common.Exceptions;
using Application.Common.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace Infrastructure.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly LogTraceBase _logTrace;
        private readonly IEmailService _emailSender;
        private readonly ApplicationConfig _applicationConfig;
        private readonly UserManager<ApplicationUser> _userManager;

        public IdentityService(UserManager<ApplicationUser> userManager, IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig, LogTraceBase logTrace)
        {
            _logTrace = logTrace;
            _emailSender = emailSender;
            _userManager = userManager;
            _applicationConfig = applicationConfig.Value;
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
                _logTrace.Log(new LogEntry(LogLevel.Error, MethodBase.GetCurrentMethod(), result.Errors));

                var duplicateErr = result.Errors.FirstOrDefault(x => x.Code == "DuplicateUserName");
                if (duplicateErr != null)
                {
                    var regex = new Regex(Regex.Escape("Username"));
                    throw new ConflictException(new { email = regex.Replace(duplicateErr.Description, "Email", 1) });
                }

                throw new Exception($"UnhandledException: {GetType().Name}");
            }

            _logTrace.Log(new LogEntry(LogLevel.Information, MethodBase.GetCurrentMethod(), "User created a new account with password."));

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = Base64UrlEncode(code);
            var callbackUrl = $"{_applicationConfig.Endpoint}/api/auth/confirm-email?userId={userId}&code={code}";

            await _emailSender.SendEmailAsync(request.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

            return user.Id;
        }

        private static string Base64UrlEncode(string input)
        {
            var inputBytes = Encoding.UTF8.GetBytes(input);
            // Special "url-safe" base64 encode.
            return Convert.ToBase64String(inputBytes)
              .Replace('+', '-')
              .Replace('/', '_')
              .Replace("=", "");
        }
    }
}
