using Auth.Application.Common.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ResetPassword
{
    public record ResetPasswordCommand(string Email, string Password, string ConfirmPassword, string Code)
        : IRequest;

    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
    {
        private readonly IIdentityService _identityService;

        public ResetPasswordCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            return _identityService.ResetPasswordAsync(request);
        }
    }
}