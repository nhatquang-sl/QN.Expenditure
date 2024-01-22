using Application.Common.Logging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class LogTrace : LogTraceBase
    {
        private readonly ILogger<LogTrace> _logger;

        public LogTrace(ILogger<LogTrace> logger)
        {
            _logger = logger;
        }

        public override void Flush()
        {
            if (!_entries.Any()) return;

            Console.WriteLine(_entries);
            var logLevel = _entries.Max(x => x.Level);
            _logger.Log(logLevel, "{@Entries}", _entries.Select(x => new { Message = $"[{x.Level}] {x.Message}", x.Data }));
        }
    }
}
