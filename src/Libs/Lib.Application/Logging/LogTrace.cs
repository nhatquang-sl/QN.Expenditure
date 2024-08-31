using Microsoft.Extensions.Logging;
using Serilog.Events;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace Lib.Application.Logging
{
    public class LogTrace(Serilog.Core.Logger logger) : ILogTrace
    {
        private readonly Serilog.Core.Logger _logger = logger;
        private readonly ExpandoObject _properties = new();
        private readonly List<LogEntry> _entries = [];

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
            => _entries.Add(new LogEntry(LogLevel.Critical, message, data, methodBase));

        public void Flush()
        {
            if (!_entries.Any()) return;

            var logLevel = _entries.Max(x => x.Level);
            var entries = _entries.Select(x =>
            {
                HideSensitiveData(x.Data);
                //if (x.Data != null)
                //{
                //    // hide user password
                //    foreach (PropertyInfo prop in x.Data.GetType().GetProperties())
                //    {
                //        if (prop.Name.Equals("password", StringComparison.OrdinalIgnoreCase)
                //        || prop.Name.Equals("SecretKey", StringComparison.OrdinalIgnoreCase)
                //        || prop.Name.Equals("apikey", StringComparison.OrdinalIgnoreCase))
                //        {
                //            prop.SetValue(x.Data, "*");
                //        }
                //    }
                //}

                return new { Message = $"[{x.Level}] {x.Message}", x.Data };
            }).ToList();
            _properties.TryAdd("Entries", entries);
            _properties.TryAdd("SourceContext", GetType().FullName);

            string msgTemplate = string.Join(" ", _properties.Select(x => "{@" + x.Key + "}"));
            var args = _properties.Select(x => x.Value).ToArray();
            //_logger.Log(logLevel, msgTemplate, JsonSerializer.Serialize(args));

            _logger.Write(Convert(logLevel), msgTemplate, args);
        }

        string[] ourNamespaces = ["Domain", "Application", "Infrastructure"];
        object? HideSensitiveData(object? data)
        {
            try
            {
                if (data == null || data.GetType().Namespace == null || !ourNamespaces.Any(x => data.GetType().Namespace.Contains(x))) return data;
                foreach (PropertyInfo prop in data.GetType().GetProperties())
                {
                    prop.SetValue(data, HideSensitiveData(prop.GetValue(data)));
                    if (prop.Name.Equals("password", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.Equals("SecretKey", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.Equals("apikey", StringComparison.OrdinalIgnoreCase))
                    {
                        prop.SetValue(data, "*");
                    }
                }
            }
            catch (Exception e)
            {

            }

            return data;
        }

        LogEventLevel Convert(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Critical => LogEventLevel.Fatal,
                _ => LogEventLevel.Verbose,
            };
        }
    }
}
