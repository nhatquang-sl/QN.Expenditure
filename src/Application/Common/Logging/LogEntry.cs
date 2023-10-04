using System.Reflection;

namespace Application.Common.Logging
{
    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public object Data { get; set; }
        public string Message { get; set; }

        public LogEntry(LogLevel level, string message, object data)
        {
           Level = level;
           Message = message;
           Data = data;
        }

        public LogEntry(LogLevel level, MethodBase? methodBase, object? data)
        {
            Level = level;
            Message = $"{methodBase?.ReflectedType?.ReflectedType?.Name}.{methodBase?.ReflectedType?.Name}";
            Data = data;
        }
    }
}
