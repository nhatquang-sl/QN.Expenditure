using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Configs;
using MediatR;
using System.Text.Encodings.Web;

namespace Application.Auth.Commands.Register
{
    public record RegisterSuccessEvent(UserProfileDto User, string Code) : INotification;

    public class SendRegisterConfirmEmailEventHandler : INotificationHandler<RegisterSuccessEvent>
    {
        private readonly IEmailService _emailSender;
        private readonly ApplicationConfig _applicationConfig;

        public SendRegisterConfirmEmailEventHandler(IEmailService emailSender, ApplicationConfig applicationConfig)
        {
            _emailSender = emailSender;
            _applicationConfig = applicationConfig;
        }

        public async Task Handle(RegisterSuccessEvent notification, CancellationToken cancellationToken)
        {
            var callbackUrl = $"{_applicationConfig.Endpoint}/api/auth/confirm-email?userId={notification.User.Id}&code={notification.Code}";

            await _emailSender.SendEmailAsync(notification.User.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}
