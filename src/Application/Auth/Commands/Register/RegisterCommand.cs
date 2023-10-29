using Application.Common.Abstractions;
using Application.Common.Logging;
using MediatR;
using System.Reflection;

namespace Application.Auth.Commands.Register
{
    public class RegisterCommand : IRequest<RegisterResult>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class RegisterResult
    {
        public RegisterResult(string userId)
        {
            UserId = userId;
        }

        public string UserId { get; set; }
    }

    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
    {
        private readonly IPublisher _publisher;
        private readonly LogTraceBase _logTrace;
        private readonly IIdentityService _identityService;

        public RegisterCommandHandler(IPublisher publisher, LogTraceBase logTrace, IIdentityService identityService)
        {
            _publisher = publisher;
            _logTrace = logTrace;
            _identityService = identityService;
        }

        public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var (user, code) = await _identityService.CreateUserAsync(request);

            _logTrace.Log(new LogEntry(LogLevel.Information, MethodBase.GetCurrentMethod(), new { user.Id }));

            await _publisher.Publish(new RegisterSuccessEvent(user, code), cancellationToken);
            return new RegisterResult(user.Id);
        }
    }
}
