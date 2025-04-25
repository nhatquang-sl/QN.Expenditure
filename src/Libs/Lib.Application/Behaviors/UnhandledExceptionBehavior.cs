using System.Text;
using Lib.Application.Abstractions;
using Lib.Application.Configs;
using Lib.Application.Logging;
using MediatR;
using Microsoft.Extensions.Options;

namespace Lib.Application.Behaviors
{
    public class UnhandledExceptionBehavior<TRequest, TResponse>(
        ILogTrace logTrace,
        INotifier notifier,
        IOptions<ApplicationConfig> applicationConfig)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            try
            {
                logTrace.LogInformation($"Started {request.GetType().Name} at : {DateTime.UtcNow}");
                return await next();
            }
            catch (Exception ex)
            {
                var e = ex;
                var sb = new StringBuilder();
                while (e != null)
                {
                    sb.AppendLine(e.Message);
                    e = e.InnerException;
                }

                logTrace.LogError(request.GetType().Name, request);
                logTrace.LogError(sb.ToString(), new { ex.StackTrace });
                await notifier.NotifyError(request.GetType().Name, ex, cancellationToken);
                throw;
            }
            finally
            {
                logTrace.AddProperty("RequestName", request.GetType().Name);
                logTrace.AddProperty("ConfigVersion", applicationConfig.Value.Version);
                logTrace.Flush();
            }
        }
    }
}