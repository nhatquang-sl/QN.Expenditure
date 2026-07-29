using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Auth.Domain.Entities;
using Lib.Application.Extensions;
using MediatR;

namespace Auth.Application.Account.Commands.Login
{
    public class LoginCommand : IRequest<UserAuthDto>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public class LoginCommandHandler(
        IIdentityService identityService,
        IJwtProvider jwtService,
        IAuthDbContext dbContext)
        : IRequestHandler<LoginCommand, UserAuthDto>
    {
        public async Task<UserAuthDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var userProfile =
                await identityService.LoginAsync(request.Email, request.Password);

            var (accessToken, refreshToken, accessTokenExpires, refreshTokenExpires) =
                jwtService.GenerateTokens(userProfile, request.RememberMe);

            var userAuth = new UserAuthDto
            {
                Id = userProfile.Id,
                Email = userProfile.Email,
                FirstName = userProfile.FirstName,
                LastName = userProfile.LastName,
                EmailConfirmed = userProfile.EmailConfirmed,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpires = accessTokenExpires.ToUnixTimestampMilliseconds(),
                RefreshTokenExpires = refreshTokenExpires.ToUnixTimestampMilliseconds()
            };

            await dbContext.UserLoginHistories.AddAsync(new UserLoginHistory
            {
                UserId = userProfile.Id,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RememberMe = request.RememberMe,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return userAuth;
        }
    }
}
