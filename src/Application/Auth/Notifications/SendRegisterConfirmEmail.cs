using Application.Auth.Commands.Register;
using Application.Auth.Commands.ResendEmailConfirmation;
using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Configs;
using MediatR;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace Application.Auth.Notifications
{
    public class SendRegisterConfirmEmail : INotificationHandler<RegisterEvent>, INotificationHandler<ResendEmailConfirmationEvent>
    {
        private readonly IEmailService _emailSender;
        private readonly ApplicationConfig _applicationConfig;

        public SendRegisterConfirmEmail(IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig)
        {
            _emailSender = emailSender;
            _applicationConfig = applicationConfig.Value;
        }

        public Task Handle(RegisterEvent notification, CancellationToken cancellationToken)
            => SendEmailAsync(notification.User, notification.Code);

        public Task Handle(ResendEmailConfirmationEvent notification, CancellationToken cancellationToken)
            => SendEmailAsync(notification.User, notification.Code);

        async Task SendEmailAsync(UserProfileDto user, string code)
        {
            var callbackUrl = $"{_applicationConfig.Endpoint}/api/auth/confirm-email?userId={user.Id}&code={code}";

            await _emailSender.SendEmailAsync(user.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}
