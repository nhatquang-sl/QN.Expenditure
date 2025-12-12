using System.Text.Encodings.Web;
using Auth.Application.Account.Commands.Register;
using Auth.Application.Account.Commands.ResendEmailConfirmation;
using Auth.Application.Account.DTOs;
using Lib.Application.Configs;
using Lib.ExternalServices.Email;
using MediatR;
using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Notifications
{
    public class SendRegisterConfirmEmail(IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig)
        : INotificationHandler<RegisterEvent>,
            INotificationHandler<ResendEmailConfirmationEvent>
    {
        private readonly ApplicationConfig _applicationConfig = applicationConfig.Value;

        public Task Handle(RegisterEvent notification, CancellationToken cancellationToken)
        {
            return SendEmailAsync(notification.User, notification.Code);
        }

        public Task Handle(ResendEmailConfirmationEvent notification, CancellationToken cancellationToken)
        {
            return SendEmailAsync(notification.User, notification.Code);
        }

        private async Task SendEmailAsync(UserProfileDto user, string code)
        {
            var callbackUrl = $"{_applicationConfig.Endpoint}/register-confirm?userId={user.Id}&code={code}";

            await emailSender.SendEmailAsync(user.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}