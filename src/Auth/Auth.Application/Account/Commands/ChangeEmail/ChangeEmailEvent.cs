using Lib.Application.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ChangeEmail
{
    public record ChangeEmailEvent(ICurrentUser User, string Code, string NewEmail) : INotification;
}