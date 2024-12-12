using Auth.Application.Common.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ConfirmEmail
{
    public class ConfirmEmailCommand : IRequest<bool>
    {
        public ConfirmEmailCommand(string userId, string code)
        {
            UserId = userId;
            Code = code;
        }

        public string UserId { get; set; }
        public string Code { get; set; }
    }

    public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, bool>
    {
        private readonly IIdentityService _identityService;

        public ConfirmEmailCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public Task<bool> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
        {
            return _identityService.ConfirmEmailAsync(request.UserId, request.Code);
        }
    }
}