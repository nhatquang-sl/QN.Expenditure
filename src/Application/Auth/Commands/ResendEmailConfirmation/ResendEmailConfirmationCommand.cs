using Application.Auth.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;

namespace Application.Auth.Commands.ResendEmailConfirmation
{
    public record ResendEmailConfirmationCommand() : IRequest;


    public class ResendEmailConfirmationCommandHandler : IRequestHandler<ResendEmailConfirmationCommand>
    {
        private readonly IMapper _mapper;
        private readonly IPublisher _publisher;
        private readonly ICurrentUser _currentUser;
        private readonly IIdentityService _identityService;

        public ResendEmailConfirmationCommandHandler(IMapper mapper, IPublisher publisher
            , ICurrentUser currentUser, IIdentityService identityService)
        {
            _mapper = mapper;
            _publisher = publisher;
            _currentUser = currentUser;
            _identityService = identityService;
        }

        public async Task Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
        {
            var code = await _identityService.GenerateEmailConfirmCode(_currentUser.Id);

            await _publisher.Publish(new ResendEmailConfirmationEvent(_mapper.Map<UserProfileDto>(_currentUser), code), cancellationToken);
        }
    }
}
