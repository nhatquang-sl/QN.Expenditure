using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Application.Common.Logging
{
    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public object? Data { get; set; } = null;
        public string Message { get; set; }

        public LogEntry(LogLevel level, string message, object data)
        {
            Level = level;
            Message = message;
            Data = data;
        }

        public LogEntry(LogLevel level, string message, object? data, MethodBase? methodBase)
        {
            Level = level;
            if (methodBase != null)
                Message = $"{methodBase?.ReflectedType?.ReflectedType?.Name}.{methodBase?.ReflectedType?.Name}{(string.IsNullOrWhiteSpace(message) ? "" : $" - {message}")}";
            else
                Message = $"{message}";
            Data = data;
        }

        public LogEntry(LogLevel level, object? data, MethodBase? methodBase) : this(level, "", data, methodBase) { }

        public LogEntry(LogLevel level, string message, MethodBase? methodBase) : this(level, message, null, methodBase) { }
    }
}
