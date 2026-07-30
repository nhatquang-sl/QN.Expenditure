using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lib.Application.Behaviors;

public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            return await next(cancellationToken);
        }
        finally
        {
            timer.Stop();
            var elapsedMilliseconds = timer.ElapsedMilliseconds;
            if (elapsedMilliseconds > 500)
                logger.LogWarning("Processed Time: {RequestName} ({ElapsedMilliseconds} milliseconds)", RequestName, elapsedMilliseconds);
            else
                logger.LogInformation("Processed Time: {RequestName} ({ElapsedMilliseconds} milliseconds)", RequestName, elapsedMilliseconds);
        }
    }
}
