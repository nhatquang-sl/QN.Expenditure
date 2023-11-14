using Application.Auth.Commands.ChangeEmail;
using Application.Auth.Commands.ChangePassword;
using Application.Auth.Commands.ConfirmEmailChange;
using Application.Auth.Commands.ForgotPassword;
using Application.Auth.Commands.Register;
using Application.Auth.Commands.ResetPassword;
using Application.Auth.DTOs;

namespace Application.Common.Abstractions
{
    public interface IIdentityService
    {
        Task<(UserProfileDto, string)> CreateUserAsync(RegisterCommand request);
        Task<bool> ConfirmEmailAsync(string userId, string code);
        Task<string> ConfirmEmailChangeAsync(ConfirmEmailChangeCommand request);
        Task<UserProfileDto> LoginAsync(string email, string password, bool rememberMe);
        Task<string> ChangePassword(string userId, ChangePasswordCommand request);
        Task<string> ChangeEmail(string userId, ChangeEmailCommand request);
        Task<string> ForgotPasswordAsync(ForgotPasswordCommand request);
        Task ResetPasswordAsync(ResetPasswordCommand request);
    }
}
