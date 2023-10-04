using Application.Common.Abstractions;
using Application.Common.Logging;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Mailjet.Client.TransactionalEmails;
using Mailjet.Client.TransactionalEmails.Response;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter()
            }
        });
        private readonly LogTraceBase _logTrace;
        private readonly EmailConfig _emailConfig;
        private readonly IMailjetClient _mailClient;

        public EmailService(IMailjetClient mailClient, IOptions<EmailConfig> emailConfig, LogTraceBase logTrace)
        {
            _logTrace = logTrace;
            _mailClient = mailClient;
            _emailConfig = emailConfig.Value;
        }

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
                }, Serializer)
            };

            var res = await _mailClient.PostAsync(request);
            if (!res.IsSuccessStatusCode)
                _logTrace.Log(new LogEntry(LogLevel.Error, MethodBase.GetCurrentMethod(), res.Content));
            else
                _logTrace.Log(new LogEntry(LogLevel.Information, MethodBase.GetCurrentMethod(), res.Content.ToObject<TransactionalEmailResponse>()?.Messages.FirstOrDefault()));
        }
    }

    public class EmailConfig
    {
        public string FromEmail { get; set; }
        public string ApiKeyPublic { get; set; }
        public string ApiKeyPrivate { get; set; }
    }
}
