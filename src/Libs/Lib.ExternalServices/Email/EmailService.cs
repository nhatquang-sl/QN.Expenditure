using System.Text.Json;
using System.Text.Json.Serialization;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Mailjet.Client.TransactionalEmails;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Lib.ExternalServices.Email
{
    public class EmailService(IMailjetClient mailClient, IOptions<EmailConfig> emailConfig)
        : IEmailService
    {
        private static readonly JsonSerializerOptions Serializer = new()
        {
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingDefault, // Equivalent to DefaultValueHandling.Ignore
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) // Equivalent to StringEnumConverter()
            }
        };

        private readonly EmailConfig _emailConfig = emailConfig.Value;

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // construct your email with builder
            var transactionEmail = new TransactionalEmailBuilder()
                .WithFrom(new SendContact(_emailConfig.FromEmail))
                .WithSubject(subject)
                .WithHtmlPart(htmlMessage)
                .WithTo(new SendContact(email))
                .Build();

            MailjetRequest request = new()
            {
                Resource = SendV31.Resource,
                Body = JObject.FromObject(new SendEmailRequest
                {
                    Messages = new List<TransactionalEmail> { transactionEmail }
                })
            };

            var res = await mailClient.PostAsync(request);
            if (!res.IsSuccessStatusCode)
            {
                // _logTrace.Log(new LogEntry(LogLevel.Error, res.Content, MethodBase.GetCurrentMethod()));
            }
            // _logTrace.Log(new LogEntry(LogLevel.Information,
            //     res.Content.ToObject<TransactionalEmailResponse>()?.Messages.FirstOrDefault() ??
            //     new MessageResult(), MethodBase.GetCurrentMethod()));
        }
    }
}