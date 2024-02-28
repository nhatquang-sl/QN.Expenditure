using Application.Common.Logging;
using MediatR;
using System.Text;

namespace Application.Common.Behaviors
{
    public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogTrace _logTrace;

        public UnhandledExceptionBehavior(ILogTrace logTrace)
        {
            _logTrace = logTrace;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                _logTrace.LogInformation($"Started {request.GetType().Name} at : {DateTime.UtcNow}");
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
                _logTrace.LogError(sb.ToString(), new { stackTrace = ex.StackTrace });
                throw;
            }
            finally
            {
                _logTrace.AddProperty("RequestName", request.GetType().Name);
                _logTrace.Flush();
            }
        }
    }
}
