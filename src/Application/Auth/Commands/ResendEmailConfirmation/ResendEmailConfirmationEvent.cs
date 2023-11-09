using Application.Auth.DTOs;
using MediatR;

namespace Application.Auth.Commands.ResendEmailConfirmation
{
    public record ResendEmailConfirmationEvent(UserProfileDto User, string Code) : INotification;
}
