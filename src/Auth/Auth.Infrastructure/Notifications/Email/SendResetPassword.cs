using System.Text.Encodings.Web;
using Application.Auth.Commands.ForgotPassword;
using Lib.Application.Configs;
using Lib.ExternalServices.Email;
using MediatR;
using Microsoft.Extensions.Options;

namespace Auth.Application.Account.Notifications.Email
{
    public class SendResetPassword(IEmailService emailSender, IOptions<ApplicationConfig> applicationConfig)
        : INotificationHandler<ForgotPasswordEvent>
    {
        private readonly ApplicationConfig _applicationConfig = applicationConfig.Value;

        public async Task Handle(ForgotPasswordEvent notification, CancellationToken cancellationToken)
        {
            var callbackUrl = $"{_applicationConfig.Endpoint}/api/auth/reset-password?code={notification.Code}";

            await emailSender.SendEmailAsync(
                notification.Email,
                "Reset Password",
                $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
        }
    }
}