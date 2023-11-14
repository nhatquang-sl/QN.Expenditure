using Application.Common.Abstractions;
using Application.Common.Logging;
using MediatR;
using System.Reflection;

namespace Application.Auth.Commands.ForgotPassword
{
    public record ForgotPasswordCommand(string Email) : IRequest;

    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
    {
        private readonly IPublisher _publisher;
        private readonly LogTraceBase _logTrace;
        private readonly IIdentityService _identityService;

        public ForgotPasswordCommandHandler(IPublisher publisher, LogTraceBase logTrace, IIdentityService identityService)
        {
            _publisher = publisher;
            _logTrace = logTrace;
            _identityService = identityService;
        }

        public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var code = await _identityService.ForgotPasswordAsync(request);

            _logTrace.Log(new LogEntry(LogLevel.Information, MethodBase.GetCurrentMethod(), new { request.Email }));

            await _publisher.Publish(new ForgotPasswordEvent(request.Email, code), cancellationToken);
        }
    }
}
