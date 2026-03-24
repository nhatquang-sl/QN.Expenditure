using Serilog.Core;
using Serilog.Events;

namespace WebAPI.Middleware;

public class HttpContextEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "IPAddress", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "UserAgent", httpContext.Request.Headers.UserAgent.ToString()));
    }
}
