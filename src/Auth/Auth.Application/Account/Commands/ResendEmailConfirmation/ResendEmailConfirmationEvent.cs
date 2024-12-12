using Auth.Application.Account.DTOs;
using MediatR;

namespace Auth.Application.Account.Commands.ResendEmailConfirmation
{
    public record ResendEmailConfirmationEvent(UserProfileDto User, string Code) : INotification;
}