using System.Reflection;
using Auth.Application.Common.Abstractions;
using Lib.Application.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Account.Commands.Register
{
    public class RegisterCommand
        : IRequest<RegisterResult>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class RegisterResult(string userId)
    {
        public string UserId { get; } = userId;
    }

    public class RegisterCommandHandler(IPublisher publisher, ILogTrace logTrace, IIdentityService identityService)
        : IRequestHandler<RegisterCommand, RegisterResult>
    {
        public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var (user, code) = await identityService.CreateUserAsync(request);

            logTrace.Log(new LogEntry(LogLevel.Information, new { user.Id }, MethodBase.GetCurrentMethod()));

            await publisher.Publish(new RegisterEvent(user, code), cancellationToken);
            return new RegisterResult(user.Id);
        }
    }
}