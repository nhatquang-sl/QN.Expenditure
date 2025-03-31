using Lib.Application.Logging;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lib.Application.Behaviors
{
    public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Stopwatch _timer;
        private readonly ILogTrace _logTrace;

        public PerformanceBehavior(ILogTrace logTrace)
        {
            _timer = new Stopwatch();
            _logTrace = logTrace;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _timer.Start();
            try
            {
                var response = await next();

                return response;
            }
            finally
            {
                _timer.Stop();

                var elapsedMilliseconds = _timer.ElapsedMilliseconds;

                var requestName = typeof(TRequest).Name;
                var logLevel = elapsedMilliseconds > 500 ? LogLevel.Warning : LogLevel.Information;
                _logTrace.Log(new LogEntry(logLevel, $"Processed Time: {requestName} ({elapsedMilliseconds} milliseconds)",null));
            }
        }
    }
}
