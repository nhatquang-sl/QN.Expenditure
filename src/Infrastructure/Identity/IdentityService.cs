using Application.Auth.Commands.ChangeEmail;
using Application.Auth.Commands.ChangePassword;
using Application.Auth.Commands.ConfirmEmailChange;
using Application.Auth.Commands.ForgotPassword;
using Application.Auth.Commands.Register;
using Application.Auth.Commands.ResetPassword;
using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Logging;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
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
                _logTrace.Log(new LogEntry(LogLevel.Error, result.Errors, MethodBase.GetCurrentMethod()));

                var duplicateErr = result.Errors.FirstOrDefault(x => x.Code == "DuplicateUserName");
                if (duplicateErr != null)
                {
                    var regex = new Regex(Regex.Escape("Username"));
                    throw new ConflictException(new { email = regex.Replace(duplicateErr.Description, "Email", 1) });
                }

                throw new Exception($"UnhandledException: {GetType().Name}");
            }

            _logTrace.Log(new LogEntry(LogLevel.Information, "User created a new account with password.", MethodBase.GetCurrentMethod()));

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
                var logEntry = new LogEntry(LogLevel.Error, message, MethodBase.GetCurrentMethod());
                _logTrace.Log(logEntry);
                throw new NotFoundException(message);
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);

            if (!result.Succeeded)
            {
                _logTrace.Log(new LogEntry(LogLevel.Error, result.Errors, MethodBase.GetCurrentMethod()));
            }

            return result.Succeeded;
        }

        public async Task<string> ConfirmEmailChangeAsync(ConfirmEmailChangeCommand request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                throw new NotFoundException($"Unable to load user with ID '{request.UserId}'.");
            }

            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
            var result = await _userManager.ChangeEmailAsync(user, request.Email, code);
            if (!result.Succeeded)
            {
                throw new BadRequestException("Error changing email.");
            }

            // In our UI email and user name are one and the same, so when we update the email
            // we need to update the user name.
            var setUserNameResult = await _userManager.SetUserNameAsync(user, request.Email);
            if (!setUserNameResult.Succeeded)
            {
                throw new BadRequestException("Error changing user name.");
            }

            await _signInManager.RefreshSignInAsync(user);
            return "Thank you for confirming your email change.";
        }

        public async Task<UserProfileDto> LoginAsync(string email, string password, bool rememberMe)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
            if (result.Succeeded || result.IsNotAllowed)
            {
                _logTrace.Log(new LogEntry(LogLevel.Information, "User logged in.", MethodBase.GetCurrentMethod()));
                //_logTrace.Log(new LogEntry(LogLevel.Information, "User logged in.", new { email }, MethodBase.GetCurrentMethod()));
                var user = await _userManager.FindByEmailAsync(email);

                return user == null
                    ? throw new NotFoundException($"{email} is not found!")
                    : new UserProfileDto
                    {
                        Id = user.Id,
                        Email = email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        EmailConfirmed = user.EmailConfirmed
                    };
            }
            if (result.RequiresTwoFactor)
            {
                // return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = request.RememberMe });
                throw new BadRequestException("Requires Two Factor.");
            }
            if (result.IsLockedOut)
            {
                _logTrace.Log(new LogEntry(LogLevel.Error, "User account locked out.", MethodBase.GetCurrentMethod()));
                throw new BadRequestException("User account locked out.");
            }

            _logTrace.Log(new LogEntry(LogLevel.Error, "Invalid login attempt.", MethodBase.GetCurrentMethod()));
            throw new BadRequestException("Email or Password incorrect.");
        }

        public async Task<string> ChangePassword(string userId, ChangePasswordCommand request)
        {
            var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException($"User is not found!");
            var changePasswordResult = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                _logTrace.Log(LogLevel.Error, changePasswordResult, MethodBase.GetCurrentMethod());
                throw new BadRequestException(changePasswordResult.Errors.First().Description);
            }

            await _signInManager.RefreshSignInAsync(user);
            _logTrace.Log(LogLevel.Information, "User changed their password successfully.", MethodBase.GetCurrentMethod());
            return "Your password has been changed.";
        }

        public async Task<string> ChangeEmail(string userId, ChangeEmailCommand request)
        {
            var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException($"User is not found!");

            if (request.NewEmail == user.Email) throw new BadRequestException("Your email is unchanged.");

            var code = await _userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            return code;
        }

        public async Task<string> ForgotPasswordAsync(ForgotPasswordCommand request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Don't reveal that the user does not exist or is not confirmed
                throw new NotFoundException("User does not exist or is not confirmed.");
            }

            // For more information on how to enable account confirmation and password reset please
            // visit https://go.microsoft.com/fwlink/?LinkID=532713
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            return code;
        }

        public async Task ResetPasswordAsync(ResetPasswordCommand request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new NotFoundException($"User is not found!");
            var result = await _userManager.ResetPasswordAsync(user, request.Code, request.Password);
            if (!result.Succeeded)
            {
                throw new BadRequestException(result.Errors.First().Description);
            }
        }
    }
}
