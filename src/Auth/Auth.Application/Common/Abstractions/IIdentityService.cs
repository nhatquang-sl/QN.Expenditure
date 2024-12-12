using Auth.Application.Account.Commands.ChangeEmail;
using Auth.Application.Account.Commands.ChangePassword;
using Auth.Application.Account.Commands.ConfirmEmailChange;
using Auth.Application.Account.Commands.ForgotPassword;
using Auth.Application.Account.Commands.Register;
using Auth.Application.Account.Commands.ResetPassword;
using Auth.Application.Account.DTOs;

namespace Auth.Application.Common.Abstractions
{
    public interface IIdentityService
    {
        Task<(UserProfileDto, string)> CreateUserAsync(RegisterCommand request);
        Task<string> GenerateEmailConfirmCode(string userId);
        Task<bool> ConfirmEmailAsync(string userId, string code);
        Task<string> ConfirmEmailChangeAsync(ConfirmEmailChangeCommand request);
        Task<UserProfileDto> LoginAsync(string email, string password, bool rememberMe);
        Task<string> ChangePassword(string userId, ChangePasswordCommand request);
        Task<string> ChangeEmail(string userId, ChangeEmailCommand request);
        Task<string> ForgotPasswordAsync(ForgotPasswordCommand request);
        Task ResetPasswordAsync(ResetPasswordCommand request);
    }
}