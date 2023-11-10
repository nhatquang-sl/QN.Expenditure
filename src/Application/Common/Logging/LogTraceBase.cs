using System.Reflection;

namespace Application.Common.Logging
{
    public abstract class LogTraceBase
    {
        protected readonly IList<LogEntry> _entries = new List<LogEntry>();

        public void Log(LogEntry entry)
        {
            _entries.Add(entry);
        }

        public void Log(LogLevel level, MethodBase? methodBase, object? data)
        {
            _entries.Add(new LogEntry(level, methodBase, data));
        }

        public abstract void Flush();
    }
}
