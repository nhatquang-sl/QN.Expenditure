using Application.Auth.Commands.ForgotPassword;
using Application.Common.Abstractions;
using Application.Common.Configs;
using MediatR;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace Application.Auth.Notifications.Email
{
    public class SendResetPassword : INotificationHandler<ForgotPasswordEvent>
    {
        private readonly IEmailService _emailSender;
        private readonly ApplicationConfig _applicationConfig;

        public SendResetPassword(IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig)
        {
            _emailSender = emailSender;
            _applicationConfig = applicationConfig.Value;
        }

        public async Task Handle(ForgotPasswordEvent notification, CancellationToken cancellationToken)
        {
            var callbackUrl = $"{_applicationConfig.Endpoint}/api/auth/reset-password?code={notification.Code}";

            await _emailSender.SendEmailAsync(
                notification.Email,
                "Reset Password",
                $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}
