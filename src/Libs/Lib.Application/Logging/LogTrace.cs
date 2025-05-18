using System.Dynamic;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Lib.Application.Logging
{
    public class LogTrace(ILogger<LogTrace> logger) : ILogTrace
    {
        private static readonly string[] OurNamespaces = ["Domain", "Application", "Infrastructure"];
        private readonly List<LogEntry> _entries = [];
        private readonly ExpandoObject _properties = new();

        public void AddProperty(string key, object value)
        {
            _properties.TryAdd(key, value);
        }

        public object GetProperty(string key)
        {
            return _properties.First(x => x.Key == key).Value;
        }

        public void Log(LogEntry entry)
        {
            _entries.Add(entry);
        }

        public void LogDebug(string message, object? data = null, MethodBase? methodBase = null)
        {
            _entries.Add(new LogEntry(LogLevel.Debug, message, data, methodBase));
        }

        public void LogDebug(object data, MethodBase? methodBase = null)
        {
            _entries.Add(new LogEntry(LogLevel.Debug, string.Empty, data, methodBase));
        }

        public void LogInformation(string message, object? data = null, MethodBase? methodBase = null)
        {
            _entries.Add(new LogEntry(LogLevel.Information, message, data, methodBase));
        }

        public void LogWarning(string message, object? data = null, MethodBase? methodBase = null)
        {
            _entries.Add(new LogEntry(LogLevel.Warning, message, data, methodBase));
        }

        public void LogError(string message, object? data = null, MethodBase? methodBase = null)
        {
            _entries.Add(new LogEntry(LogLevel.Error, message, data, methodBase));
        }

        public void LogError(string message, Exception ex, MethodBase? methodBase = null)
        {
            var sb = new StringBuilder(ex.Message);
            var exception = ex.InnerException;
            while (exception != null)
            {
                sb.Append(exception.Message);
                exception = exception.InnerException;
            }

            _entries.Add(new LogEntry(LogLevel.Error, $"{message} - {sb}", ex.StackTrace, methodBase));
        }

        public void LogCritical(string message, object? data = null, MethodBase? methodBase = null)
        {
            _entries.Add(new LogEntry(LogLevel.Critical, message, data, methodBase));
        }

        public void Flush()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            var logLevel = _entries.Max(x => x.Level);
            var entries = _entries.Select<LogEntry, object>(x =>
            {
                HideSensitiveData(x.Data);

                var msgSb = new StringBuilder($"[{x.Level}]");
                if (!string.IsNullOrWhiteSpace(x.Message))
                {
                    msgSb.Append($"{x.Message}");
                }

                if (x.Data == null)
                {
                    return new { Message = msgSb.ToString() };
                }

                return new { Message = msgSb.ToString(), x.Data };
            }).ToArray();
            _properties.TryAdd("Entries", entries);
            // _properties.TryAdd("SourceContext", GetType().FullName);

            var msgTemplate = string.Join(" ", _properties.Select(x => "{@" + x.Key + "}"));
            var args = _properties.Select(x => x.Value).ToArray();
            logger.Log(logLevel, msgTemplate, args);

            // logger.Write(Convert(logLevel), msgTemplate, args);
        }

        private static object? HideSensitiveData(object? data)
        {
            try
            {
                var dataHasOurNamespace = data != null && OurNamespaces.Contains(data.GetType().FullName);
                if (data?.GetType().Namespace == null || !dataHasOurNamespace)
                {
                    return data;
                }

                foreach (var prop in data.GetType().GetProperties())
                {
                    prop.SetValue(data, HideSensitiveData(prop.GetValue(data)));
                    if (prop.Name.Equals("password", StringComparison.OrdinalIgnoreCase)
                        || prop.Name.Equals("SecretKey", StringComparison.OrdinalIgnoreCase)
                        || prop.Name.Equals("apikey", StringComparison.OrdinalIgnoreCase)
                        || prop.Name.Equals("AccessToken", StringComparison.OrdinalIgnoreCase)
                        || prop.Name.Equals("RefreshToken", StringComparison.OrdinalIgnoreCase))
                    {
                        prop.SetValue(data, "*");
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return data;
        }
    }
}