using System.Text.Encodings.Web;
using Auth.Application.Account.Commands.ChangeEmail;
using Lib.Application.Configs;
using Lib.ExternalServices.Email;
using MediatR;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Notifications
{
    public class SendConfirmEmailChange(IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig)
        : INotificationHandler<ChangeEmailEvent>
    {
        private readonly ApplicationConfig _applicationConfig = applicationConfig.Value;

        public async Task Handle(ChangeEmailEvent notification, CancellationToken cancellationToken)
        {
            var callbackUrl =
                $"{_applicationConfig.Endpoint}/api/auth/confirm-email-change?userId={notification.User.Id}&email={notification.NewEmail}&code={notification.Code}";

            await emailSender.SendEmailAsync(notification.NewEmail, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}