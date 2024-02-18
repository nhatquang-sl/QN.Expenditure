using Application.Common.Abstractions;
using MediatR;

namespace Application.Auth.Commands.ChangePassword
{
    public class ChangePasswordCommand : IRequest<string>
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, string>
    {
        private readonly ICurrentUser _currentUser;
        private readonly IIdentityService _identityService;

        public ChangePasswordCommandHandler(ICurrentUser currentUser, IIdentityService identityService)
        {
            _currentUser = currentUser;
            _identityService = identityService;
        }

        public Task<string> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
            => _identityService.ChangePassword(_currentUser.Id, request);
    }
}
