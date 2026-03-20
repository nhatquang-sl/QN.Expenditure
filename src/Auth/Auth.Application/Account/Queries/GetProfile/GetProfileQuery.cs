using Auth.Application.Account.DTOs;
using Lib.Application.Abstractions;
using MediatR;

namespace Auth.Application.Account.Queries.GetProfile
{
    public record GetProfileQuery : IRequest<UserProfileDto>;

    public class GetProfileQueryHandler(ICurrentUser currentUser)
        : IRequestHandler<GetProfileQuery, UserProfileDto>
    {
        public Task<UserProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
        {
            var profile = new UserProfileDto
            {
                Id = currentUser.Id,
                Email = currentUser.Email,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName,
                EmailConfirmed = currentUser.EmailConfirmed,
            };

            return Task.FromResult(profile);
        }
    }
}