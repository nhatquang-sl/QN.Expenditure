using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ResendEmailConfirmation
{
    public record ResendEmailConfirmationCommand : IRequest;


    public class ResendEmailConfirmationCommandHandler(
        IPublisher publisher,
        ICurrentUser currentUser,
        IIdentityService identityService)
        : IRequestHandler<ResendEmailConfirmationCommand>
    {
        public async Task Handle(ResendEmailConfirmationCommand request, CancellationToken cancellationToken)
        {
            var code = await identityService.GenerateEmailConfirmCode(currentUser.Id);

            await publisher.Publish(new ResendEmailConfirmationEvent(UserProfileDto.From(currentUser), code),
                cancellationToken);
        }
    }
}
