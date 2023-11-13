using Application.Auth.Commands.ChangeEmail;
using Application.Common.Abstractions;
using Application.Common.Configs;
using MediatR;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace Application.Auth.Notifications
{
    public class SendConfirmEmailChange : INotificationHandler<ChangeEmailEvent>
    {
        private readonly IEmailService _emailSender;
        private readonly ApplicationConfig _applicationConfig;

        public SendConfirmEmailChange(IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig)
        {
            _emailSender = emailSender;
            _applicationConfig = applicationConfig.Value;
        }

        public async Task Handle(ChangeEmailEvent notification, CancellationToken cancellationToken)
        {
            var callbackUrl = $"{_applicationConfig.Endpoint}/api/auth/confirm-email-change?userId={notification.User.Id}&email={notification.NewEmail}&code={notification.Code}";

            await _emailSender.SendEmailAsync(notification.NewEmail, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}
