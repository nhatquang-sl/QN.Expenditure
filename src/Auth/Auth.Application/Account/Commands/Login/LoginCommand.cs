using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Auth.Domain.Entities;
using AutoMapper;
using Lib.Application.Logging;
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
        IMapper mapper,
        ILogTrace logTrace,
        IIdentityService identityService,
        IJwtProvider jwtService,
        IAuthDbContext dbContext)
        : IRequestHandler<LoginCommand, UserAuthDto>
    {
        private readonly ILogTrace _logTrace = logTrace;

        public async Task<UserAuthDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var userProfile =
                await identityService.LoginAsync(request.Email, request.Password, request.RememberMe);

            var (accessToken, refreshToken) = jwtService.GenerateTokens(userProfile);

            var userAuth = mapper.Map<UserAuthDto>(userProfile);
            userAuth.AccessToken = accessToken;
            userAuth.RefreshToken = refreshToken;

            await dbContext.UserLoginHistories.AddAsync(new UserLoginHistory
            {
                UserId = userProfile.Id,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            return userAuth;
        }
    }
}