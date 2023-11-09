using Application.Auth.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;

namespace Application.Auth.Commands.ResendEmailConfirmation
{
    public class ResendEmailConfirmationCommand : IRequest
    {
        public string Code { get; set; }
    }

    public class ResendEmailConfirmationCommandHandler : IRequestHandler<ResendEmailConfirmationCommand>
    {
        private readonly IMapper _mapper;
        private readonly IPublisher _publisher;
        private readonly ICurrentUserService _currentUser;

        public ResendEmailConfirmationCommandHandler(IMapper mapper, IPublisher publisher
            , ICurrentUserService currentUser)
        {
            _mapper = mapper;
            _publisher = publisher;
            _currentUser = currentUser;
        }

        public Task Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
            => _publisher.Publish(new ResendEmailConfirmationEvent(_mapper.Map<UserProfileDto>(_currentUser), request.Code), cancellationToken);
    }
}
