using Application.Auth.Commands.Register;
using Application.Auth.DTOs;

namespace Application.Common.Abstractions
{
    public interface IIdentityService
    {
        Task<string> CreateUserAsync(RegisterCommand request);
        Task<bool> ConfirmEmailAsync(string userId, string code);
        Task<UserProfileDto> LoginAsync(string email, string password, bool rememberMe);
    }
}
