using Application.Common.Abstractions;
using MediatR;

namespace Application.Auth.Commands.ConfirmEmail
{
    public class ConfirmEmailCommand : IRequest<bool>
    {
        public string UserId { get; set; }
        public string Code { get; set; }

        public ConfirmEmailCommand(string userId, string code){
            UserId = userId;
            Code = code;
        }
    }

    public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand,bool>
    {
        private readonly IIdentityService _identityService;

        public ConfirmEmailCommandHandler(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        public  Task<bool> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
        {
            return  _identityService.ConfirmEmailAsync(request.UserId, request.Code);
        }
    }
}