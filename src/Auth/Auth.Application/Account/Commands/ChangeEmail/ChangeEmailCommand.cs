using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;

namespace Auth.Application.Account.Commands.ChangeEmail
{
    public record ChangeEmailCommand(string NewEmail) : IRequest<string>;

    public class ChangeEmailCommandHandler(
        IPublisher publisher,
        ICurrentUser currentUser,
        IIdentityService identityService)
        : IRequestHandler<ChangeEmailCommand, string>
    {
        public async Task<string> Handle(ChangeEmailCommand request, CancellationToken cancellationToken)
        {
            var code = await identityService.ChangeEmail(currentUser.Id, request);
            await publisher.Publish(new ChangeEmailEvent(UserProfileDto.From(currentUser), code, request.NewEmail), cancellationToken);
            return "Verification email sent. Please check your email.";
        }
    }
}
