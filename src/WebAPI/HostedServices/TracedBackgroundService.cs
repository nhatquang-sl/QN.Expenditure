using System.Diagnostics;
using OpenTelemetry.Trace;

namespace WebAPI.HostedServices;

public abstract class TracedBackgroundService : BackgroundService
{
    protected readonly ActivitySource ActivitySource;

    protected TracedBackgroundService()
    {
        ActivitySource = new ActivitySource(GetType().Name);
    }

    protected async Task RunTracedAsync(
        string operationName,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        // ActivityKind.Consumer + messaging.* attributes are required to produce
        // transaction.type = "messaging" in Elastic APM's OTLP intake.
        // ActivityKind.Internal/.Server without semantic attributes both default to "unknown".
        using var activity = ActivitySource.StartActivity(operationName, ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "scheduled");   // triggers "messaging" transaction type in Elastic APM
        activity?.SetTag("messaging.operation", "process");  // OTel semantic convention for processing a job
        try
        {
            await work(cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;

        }
    }
}

public static class TracedBackgroundServiceExtensions
{
    public static IServiceCollection AddTracedHostedService<T>(this IServiceCollection services)
        where T : TracedBackgroundService
    {
        services.AddHostedService<T>();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(typeof(T).Name));
        return services;
    }
}
