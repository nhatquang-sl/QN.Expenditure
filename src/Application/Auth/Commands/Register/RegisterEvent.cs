using Application.Auth.DTOs;
using MediatR;

namespace Application.Auth.Commands.Register
{
    public record RegisterEvent(UserProfileDto User, string Code) : INotification;
}
