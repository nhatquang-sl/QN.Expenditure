using Microsoft.Extensions.Logging;
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

        public void Log(LogLevel level, object data, MethodBase methodBase)
        {
            _entries.Add(new LogEntry(level, data, methodBase));
        }

        public abstract void Flush();
    }
}
