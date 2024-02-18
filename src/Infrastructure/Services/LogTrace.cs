using Application.Common.Logging;
using Microsoft.Extensions.Logging;
using System.Dynamic;
using System.Reflection;

namespace Infrastructure.Services
{
    public class LogTrace : ILogTrace
    {
        private readonly ILogger<LogTrace> _logger;
        private readonly ExpandoObject _properties = new();
        private readonly IList<LogEntry> _entries = new List<LogEntry>();

        public LogTrace(ILogger<LogTrace> logger)
        {
            _logger = logger;
        }

        public void AddProperty(string key, object value)
        {
            _properties.TryAdd(key, value);
        }

        public void Log(LogEntry entry)
        {
            _entries.Add(entry);
        }

        public void LogDebug(string message, object? data = null, MethodBase? methodBase = null)
            => _entries.Add(new LogEntry(LogLevel.Debug, message, data, methodBase));

        public void LogInformation(string message, object? data = null, MethodBase? methodBase = null)
            => _entries.Add(new LogEntry(LogLevel.Information, message, data, methodBase));

        public void LogWarning(string message, object? data = null, MethodBase? methodBase = null)
            => _entries.Add(new LogEntry(LogLevel.Warning, message, data, methodBase));

        public void LogError(string message, object? data = null, MethodBase? methodBase = null)
            => _entries.Add(new LogEntry(LogLevel.Error, message, data, methodBase));

        public void LogCritical(string message, object? data = null, MethodBase? methodBase = null)
            => _entries.Add(new LogEntry(LogLevel.Critical, message, data, methodBase));

        public void Flush()
        {
            if (!_entries.Any()) return;

            Console.WriteLine(_entries);
            var logLevel = _entries.Max(x => x.Level);
            var entries = _entries.Select(x =>
            {
                if (x.Data != null)
                {
                    // hide user password
                    foreach (PropertyInfo prop in x.Data.GetType().GetProperties())
                    {
                        if (prop.Name.Equals("password", StringComparison.OrdinalIgnoreCase))
                        {
                            prop.SetValue(x.Data, "*");
                        }
                    }
                }

                return new { Message = $"[{x.Level}] {x.Message}", x.Data };
            });

            string msgTemplate = string.Join(" ", _properties.Select(x => "{@" + x.Key + "}").Append("{@Entries}"));
            var args = _properties.Select(x => x.Value).Append(entries).ToArray();
            _logger.Log(logLevel, msgTemplate, args);
        }
    }
}
