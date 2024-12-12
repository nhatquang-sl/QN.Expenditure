using Auth.Application.Account.DTOs;
using MediatR;

namespace Auth.Application.Account.Commands.Register
{
    public record RegisterEvent(UserProfileDto User, string Code) : INotification;
}