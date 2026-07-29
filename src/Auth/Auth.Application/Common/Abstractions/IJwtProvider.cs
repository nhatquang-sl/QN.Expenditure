using Auth.Application.Account.DTOs;

namespace Auth.Application.Common.Abstractions
{
    public interface IJwtProvider
    {
        (string accessToken, string refreshToken, DateTime accessTokenExpires, DateTime refreshTokenExpires) GenerateTokens(UserProfileDto userProfile, bool rememberMe);
        UserProfileDto? ValidateRefreshToken(string refreshToken);
    }
}