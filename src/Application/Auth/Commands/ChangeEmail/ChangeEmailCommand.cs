using Application.Auth.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;

namespace Application.Auth.Commands.ChangeEmail
{
    public class ChangeEmailCommand : IRequest<string>
    {
        public string NewEmail { get; set; }
    }

    public class ChangeEmailCommandHandler : IRequestHandler<ChangeEmailCommand, string>
    {
        private readonly IMapper _mapper;
        private readonly IPublisher _publisher;
        private readonly ICurrentUser _currentUser;
        private readonly IIdentityService _identityService;

        public ChangeEmailCommandHandler(IMapper mapper, IPublisher publisher, ICurrentUser currentUser, IIdentityService identityService)
        {
            _mapper = mapper;
            _publisher = publisher;
            _currentUser = currentUser;
            _identityService = identityService;
        }

        public async Task<string> Handle(ChangeEmailCommand request, CancellationToken cancellationToken)
        {
            var code = await _identityService.ChangeEmail(_currentUser.Id, request);
            var user = _mapper.Map<UserProfileDto>(_currentUser);
            await _publisher.Publish(new ChangeEmailEvent(user, code, request.NewEmail), cancellationToken);
            return "Verification email sent. Please check your email.";
        }
    }
}
