using Application.Auth.DTOs;
using MediatR;

namespace Application.Auth.Commands.ChangeEmail
{
    public record ChangeEmailEvent(UserProfileDto User, string Code, string NewEmail) : INotification;
}
