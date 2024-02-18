using MediatR;

namespace Application.Auth.Commands.ForgotPassword
{
    public record ForgotPasswordEvent(string Email, string Code) : INotification;
}
