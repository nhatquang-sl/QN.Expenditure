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
            Console.WriteLine(_entries);
            _logger.LogInformation("{@entries}", _entries);
        }
    }
}
