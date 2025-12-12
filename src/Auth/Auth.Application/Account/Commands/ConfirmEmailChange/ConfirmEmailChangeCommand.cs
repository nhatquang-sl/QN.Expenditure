using Auth.Application.Common.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ConfirmEmailChange
{
    public class ConfirmEmailChangeCommand : IRequest<string>
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Code { get; set; }
    }

    public class ConfirmEmailChangeCommandHandler : IRequestHandler<ConfirmEmailChangeCommand, string>
    {
        private readonly IIdentityService _identityService;

        public ConfirmEmailChangeCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public Task<string> Handle(ConfirmEmailChangeCommand request, CancellationToken cancellationToken)
        {
            return _identityService.ConfirmEmailChangeAsync(request);
        }
    }
}