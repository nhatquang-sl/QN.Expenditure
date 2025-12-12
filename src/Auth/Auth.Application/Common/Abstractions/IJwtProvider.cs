using Auth.Application.Account.DTOs;

namespace Auth.Application.Common.Abstractions
{
    public interface IJwtProvider
    {
        (string accessToken, string refreshToken) GenerateTokens(UserProfileDto userProfile);
    }
}