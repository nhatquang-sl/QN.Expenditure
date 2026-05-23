using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Auth.Domain.Entities;
using Lib.Application.Exceptions;
using Lib.Application.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Account.Commands.RefreshToken
{
    public class RefreshTokenCommand : IRequest<UserAuthDto>
    {
        public string RefreshToken { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public class RefreshTokenCommandHandler(
        IJwtProvider jwtProvider,
        IAuthDbContext dbContext)
        : IRequestHandler<RefreshTokenCommand, UserAuthDto>
    {
        public async Task<UserAuthDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            // Validate the refresh token signature and expiry
            var userProfile = jwtProvider.ValidateRefreshToken(request.RefreshToken)
                ?? throw new BadRequestException("Invalid or expired refresh token.");

            // Verify it exists in the DB (prevents reuse of revoked tokens)
            var loginHistory = await dbContext.UserLoginHistories
                .FirstOrDefaultAsync(x => x.RefreshToken == request.RefreshToken, cancellationToken)
                ?? throw new BadRequestException("Refresh token not found.");

            // Issue new tokens, preserving the original session's remember-me state
            var (accessToken, refreshToken, accessTokenExpires, refreshTokenExpires) =
                jwtProvider.GenerateTokens(userProfile, loginHistory.RememberMe);

            // Rotate: replace old record with new one
            dbContext.UserLoginHistories.Remove(loginHistory);
            await dbContext.UserLoginHistories.AddAsync(new UserLoginHistory
            {
                UserId = userProfile.Id,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RememberMe = loginHistory.RememberMe,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return new UserAuthDto
            {
                Id = userProfile.Id,
                Email = userProfile.Email,
                FirstName = userProfile.FirstName,
                LastName = userProfile.LastName,
                EmailConfirmed = userProfile.EmailConfirmed,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpires = accessTokenExpires.ToUnixTimestampMilliseconds(),
                RefreshTokenExpires = refreshTokenExpires.ToUnixTimestampMilliseconds(),
            };
        }
    }
}
