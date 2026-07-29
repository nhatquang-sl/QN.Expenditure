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
        using var activity = ActivitySource.StartActivity(operationName, ActivityKind.Server);
        await work(cancellationToken);
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
