using Application.Auth.DTOs;
using Application.Common.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Identity
{
    public class JwtProvider : IJwtProvider
    {
        private readonly JwtConfig _jwtConfig;

        public JwtProvider(IOptions<JwtConfig> jwtConfig)
        {
            _jwtConfig = jwtConfig.Value;
        }

        public (string accessToken, string refreshToken) GenerateTokens(UserProfileDto userProfile)
        {
            var accessToken = GenerateToken(userProfile, _jwtConfig.AccessTokenSecretKey, DateTime.UtcNow.AddHours(1));
            var refreshToken = GenerateToken(userProfile, _jwtConfig.RefreshTokenSecretKey, DateTime.UtcNow.AddHours(24));

            return (accessToken, refreshToken);
        }

        private string GenerateToken(UserProfileDto userProfile, string secretKey, DateTime expires)
        {
            var claims = new Claim[] {
                new(JwtClaimNames.Id, userProfile.Id),
                new(JwtClaimNames.Email, userProfile.Email),
                new(JwtClaimNames.FirstName, userProfile.FirstName),
                new(JwtClaimNames.LastName, userProfile.LastName),
                new(JwtClaimNames.EmailConfirmed, userProfile.EmailConfirmed.ToString())
            };

            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(_jwtConfig.Issuer, _jwtConfig.Audience, claims, null, expires, signingCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public struct JwtClaimNames
    {
        public const string Id = "id";
        public const string Email = "emailCus";
        public const string FirstName = "firstName";
        public const string LastName = "lastName";
        public const string EmailConfirmed = "emailConfirmed";
    }
}
