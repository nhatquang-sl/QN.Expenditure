using Application.Auth.DTOs;

namespace Application.Common.Abstractions
{
    public interface IJwtProvider
    {
        (string accessToken, string refreshToken) GenerateTokens(UserProfileDto userProfile);
    }
}
