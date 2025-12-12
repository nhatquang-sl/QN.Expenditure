using System.Reflection;
using Application.Auth.Commands.ForgotPassword;
using Auth.Application.Common.Abstractions;
using Lib.Application.Logging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Account.Commands.ForgotPassword
{
    public record ForgotPasswordCommand(string Email) : IRequest;

    public class ForgotPasswordCommandHandler(
        IPublisher publisher,
        ILogTrace logTrace,
        IIdentityService identityService)
        : IRequestHandler<ForgotPasswordCommand>
    {
        public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var code = await identityService.ForgotPasswordAsync(request);

            logTrace.Log(new LogEntry(LogLevel.Information, new { request.Email }, MethodBase.GetCurrentMethod()));

            await publisher.Publish(new ForgotPasswordEvent(request.Email, code), cancellationToken);
        }
    }
}