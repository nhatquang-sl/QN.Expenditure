using System.Reflection;

namespace Lib.Application.Logging
{
    public interface ILogTrace
    {
        void AddProperty(string key, object value);

        void Log(LogEntry entry);

        void LogDebug(string message, object? data = null, MethodBase? methodBase = null);

        void LogInformation(string message, object? data = null, MethodBase? methodBase = null);

        void LogWarning(string message, object? data = null, MethodBase? methodBase = null);

        void LogError(string message, object? data = null, MethodBase? methodBase = null);

        void LogError(string message, Exception ex, MethodBase? methodBase = null);

        void LogCritical(string message, object? data = null, MethodBase? methodBase = null);

        public void Flush();
    }
}
