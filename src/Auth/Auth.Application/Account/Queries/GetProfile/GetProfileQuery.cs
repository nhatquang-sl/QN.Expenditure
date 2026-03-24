using Auth.Application.Account.DTOs;
using Lib.Application.Abstractions;
using MediatR;

namespace Auth.Application.Account.Queries.GetProfile
{
    public record GetProfileQuery : IRequest<UserAuthDto>;

    public class GetProfileQueryHandler(ICurrentUser currentUser)
        : IRequestHandler<GetProfileQuery, UserAuthDto>
    {
        public Task<UserAuthDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
        {
            var profile = new UserAuthDto
            {
                Id = currentUser.Id,
                Email = currentUser.Email,
                FirstName = currentUser.FirstName,
                LastName = currentUser.LastName,
                EmailConfirmed = currentUser.EmailConfirmed,
                AccessTokenExpires = currentUser.AccessTokenExpires,
                RefreshTokenExpires = currentUser.RefreshTokenExpires,
            };

            return Task.FromResult(profile);
        }
    }
}