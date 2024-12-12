using Auth.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ChangePassword
{
    public record ChangePasswordCommand(string OldPassword, string NewPassword, string ConfirmPassword)
        : IRequest<string>;

    public class ChangePasswordCommandHandler(ICurrentUser currentUser, IIdentityService identityService)
        : IRequestHandler<ChangePasswordCommand, string>
    {
        public Task<string> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            return identityService.ChangePassword(currentUser.Id, request);
        }
    }
}