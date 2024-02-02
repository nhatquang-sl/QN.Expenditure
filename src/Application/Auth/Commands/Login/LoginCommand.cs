using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Application.Common.Logging;
using AutoMapper;
using MediatR;

namespace Application.Auth.Commands.Login
{
    public class LoginCommand : IRequest<UserAuthDto>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
    }

    public class LoginCommandHandler : IRequestHandler<LoginCommand, UserAuthDto>
    {
        private readonly IMapper _mapper;
        private readonly ILogTrace _logTrace;
        private readonly IJwtProvider _jwtService;
        private readonly IIdentityService _identityService;

        public LoginCommandHandler(IMapper mapper, ILogTrace logTrace, IIdentityService identityService, IJwtProvider jwtService)
        {
            _mapper = mapper;
            _logTrace = logTrace;
            _jwtService = jwtService;
            _identityService = identityService;
        }

        public async Task<UserAuthDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var userProfile = await _identityService.LoginAsync(request.Email, request.Password, request.RememberMe);

            var (accessToken, refreshToken) = _jwtService.GenerateTokens(userProfile);

            var userAuth = _mapper.Map<UserAuthDto>(userProfile);
            userAuth.AccessToken = accessToken;
            userAuth.RefreshToken = refreshToken;

            return userAuth;
        }
    }

}
