using Application.Auth.Commands.Register;
using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Logging;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Infrastructure.Identity
{
    public class IdentityService : IIdentityService
    {
        private readonly IMapper _mapper;
        private readonly LogTraceBase _logTrace;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IdentityService(UserManager<ApplicationUser> userManager, LogTraceBase logTrace
            , SignInManager<ApplicationUser> signInManager, IMapper mapper)
        {
            _mapper = mapper;
            _logTrace = logTrace;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<(UserProfileDto, string)> CreateUserAsync(RegisterCommand request)
        {
            var user = _mapper.Map<ApplicationUser>(request);
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
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            return (_mapper.Map<UserProfileDto>(user), code);
        }

        public async Task<bool> ConfirmEmailAsync(string userId, string code)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                var message = $"Unable to load user with ID '{userId}'.";
                var logEntry = new LogEntry(LogLevel.Error, MethodBase.GetCurrentMethod(), message);
                _logTrace.Log(logEntry);
                throw new NotFoundException(message);
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);

            if (!result.Succeeded)
            {
                _logTrace.Log(new LogEntry(LogLevel.Error, MethodBase.GetCurrentMethod(), result.Errors));
            }

            return result.Succeeded;
        }

        public async Task<UserProfileDto> LoginAsync(string email, string password, bool rememberMe)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                _logTrace.Log(new LogEntry(LogLevel.Information, MethodBase.GetCurrentMethod(), "User logged in."));
                var user = await _userManager.FindByEmailAsync(email);
                return user == null
                    ? throw new NotFoundException($"{email} is not found!")
                    : new UserProfileDto
                    {
                        Id = user.Id,
                        Email = email,
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    };
            }
            if (result.RequiresTwoFactor)
            {
                // return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                throw new BadRequestException("Requires Two Factor.");
            }
            if (result.IsLockedOut)
            {
                _logTrace.Log(new LogEntry(LogLevel.Error, MethodBase.GetCurrentMethod(), "User account locked out."));
                throw new BadRequestException("User account locked out.");
            }

            _logTrace.Log(new LogEntry(LogLevel.Error, MethodBase.GetCurrentMethod(), "Invalid login attempt."));
            throw new BadRequestException("Email or Password incorrect.");
        }
    }
}
