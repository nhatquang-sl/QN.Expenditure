namespace Application.Common.Logging
{
    public abstract class LogTraceBase
    {
        protected readonly IList<LogEntry> _entries = new List<LogEntry>();

        public void Log(LogEntry entry)
        {
            _entries.Add(entry);
        }

        public abstract void Flush();
    }
}
