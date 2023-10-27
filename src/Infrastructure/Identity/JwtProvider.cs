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

        public (string accessToken, string refreshToken) GenerateTokens(TokenParam tokenInfo)
        {
            var accessToken = GenerateToken(tokenInfo, _jwtConfig.AccessTokenSecretKey, DateTime.UtcNow.AddHours(1));
            var refreshToken = GenerateToken(tokenInfo, _jwtConfig.RefreshTokenSecretKey, DateTime.UtcNow.AddHours(24));

            return (accessToken, refreshToken);
        }

        private string GenerateToken(TokenParam tokenInfo, string secretKey, DateTime expires)
        {
            var claims = new Claim[] {
                new Claim(JwtRegisteredClaimNames.Sub, tokenInfo.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, tokenInfo.EmailAddress.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, $"{tokenInfo.FirstName} {tokenInfo.LastName}"),
            //new Claim(JwtRegisteredClaimNames.Role, tokenInfo.Roles.ToString()),
            //new Claim(JwtRegisteredClaimNames.Sub, tokenInfo.LastName.ToString()),
            };

            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(_jwtConfig.Issuer, _jwtConfig.Audience, claims, null, expires, signingCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
