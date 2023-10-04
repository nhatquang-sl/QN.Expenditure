using Application.Auth.Commands.Register;

namespace Application.Common.Abstractions
{
    public interface IIdentityService
    {
        Task<string> CreateUserAsync(RegisterCommand request);
        Task<bool> ConfirmEmailAsync(string userId, string code);
    }
}
