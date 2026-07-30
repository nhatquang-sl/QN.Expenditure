using Lib.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Lib.Application.Behaviors;

public class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger,
    INotifier notifier)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = request.GetType().Name;
        logger.LogInformation("Started {RequestName} at: {StartedAt}", requestName, DateTime.UtcNow);
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in {RequestName}", requestName);
            await notifier.NotifyError(requestName, ex, cancellationToken);
            throw;
        }
    }
}