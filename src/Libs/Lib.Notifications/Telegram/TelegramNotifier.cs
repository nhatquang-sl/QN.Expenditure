using System.Reflection;
using System.Text;
using System.Text.Json;
using Lib.Application.Abstractions;
using Lib.ExternalServices.Telegram;
using Lib.ExternalServices.Telegram.Models;
using Microsoft.Extensions.Configuration;

namespace Lib.Notifications.Telegram
{
    public class TelegramNotifier : INotifier
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true // Enables formatted (pretty-print) JSON output
        };

        private readonly string _botToken;
        private readonly string _chatId;
        private readonly bool _hasEnabled;
        private readonly ITelegramService _httpClient;
        private readonly string _msgThreadId;

        public TelegramNotifier(ITelegramService httpClient, IConfiguration configuration)
        {
            var urlConfig = configuration.GetValue<string>("Notifier:PathUrl")?.Split(";");
            _httpClient = httpClient;
            _botToken = urlConfig?[0] ?? throw new InvalidOperationException();
            _chatId = urlConfig[1];
            _msgThreadId = urlConfig[2];
            _hasEnabled = !string.IsNullOrWhiteSpace(_chatId) && !string.IsNullOrWhiteSpace(_msgThreadId);
        }

        public Task NotifyInfo(string title, string description, CancellationToken cancellationToken = default)
        {
            return Notify(title, description, null, cancellationToken);
        }

        public Task NotifyInfo(string description, object data, CancellationToken cancellationToken = default)
        {
            return Notify($"{DateTime.UtcNow}: {Assembly.GetCallingAssembly()?.GetName().Name}", description, data,
                cancellationToken);
        }

        public Task NotifyError(string description, object data, CancellationToken cancellationToken = default)
        {
            var title = $"❌{DateTime.UtcNow}: {Assembly.GetCallingAssembly()?.GetName().Name}❌";
            return Notify(title, description, data, cancellationToken);
        }

        public Task NotifyError(string description, Exception ex,
            CancellationToken cancellationToken = default)
        {
            var title = $"❌{DateTime.UtcNow}: {Assembly.GetCallingAssembly()?.GetName().Name}❌";
            var sb = new StringBuilder(ex.Message);
            sb.AppendLine(ex.StackTrace);
            var exception = ex.InnerException;
            while (exception != null)
            {
                sb.Append(exception.Message);
                exception = exception.InnerException;
            }

            return Notify(title, description, sb.ToString(), cancellationToken);
        }

        private async Task Notify(string title, string? description, object? data,
            CancellationToken cancellationToken = default)
        {
            if (!_hasEnabled)
            {
                return;
            }

            var formattedText = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(title))
            {
                formattedText.AppendLine($"<b>{EscapeHtml(title)}</b>");
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                formattedText.AppendLine($"<i>{EscapeHtml(description)}</i>");
            }

            var pre = data?.ToString();
            if (data is not Exception and not string)
            {
                pre = JsonSerializer.Serialize(data, JsonOptions);
            }

            if (data is not null && !string.IsNullOrWhiteSpace(pre))
            {
                formattedText.AppendLine($"<pre>{EscapeHtml(pre)}</pre>");
            }

            var response = await _httpClient.SendMessage(_botToken,
                new TelegramTopicMessage(_chatId, _msgThreadId, formattedText.ToString()));
        }

        private static string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}