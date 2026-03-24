using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Auth.Application.Account.DTOs;
using Auth.Application.Common.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure.Identity
{
    public class JwtProvider(IOptions<JwtConfig> jwtConfig) : IJwtProvider
    {
        private readonly JwtConfig _jwtConfig = jwtConfig.Value;

        public static int AccessTokenExpiresMinutes => 1;
        public static int RefreshTokenExpiresMinutes => 24 * 60;

        public (string accessToken, string refreshToken, DateTime accessTokenExpires, DateTime refreshTokenExpires) GenerateTokens(UserProfileDto userProfile)
        {
            var accessTokenExpires = DateTime.UtcNow.AddMinutes(AccessTokenExpiresMinutes);
            var refreshTokenExpires = DateTime.UtcNow.AddMinutes(RefreshTokenExpiresMinutes);

            var accessToken = GenerateToken(userProfile, _jwtConfig.AccessTokenSecretKey, accessTokenExpires, refreshTokenExpires);
            var refreshToken = GenerateToken(userProfile, _jwtConfig.RefreshTokenSecretKey, refreshTokenExpires, null);

            return (accessToken, refreshToken, accessTokenExpires, refreshTokenExpires);
        }

        public UserProfileDto? ValidateRefreshToken(string refreshToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(refreshToken, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtConfig.Issuer,
                    ValidAudience = _jwtConfig.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.RefreshTokenSecretKey)),
                    ClockSkew = TimeSpan.Zero
                }, out _);

                return new UserProfileDto
                {
                    Id = principal.FindFirstValue(JwtClaimNames.Id) ?? string.Empty,
                    Email = principal.FindFirstValue(JwtClaimNames.Email) ?? string.Empty,
                    FirstName = principal.FindFirstValue(JwtClaimNames.FirstName) ?? string.Empty,
                    LastName = principal.FindFirstValue(JwtClaimNames.LastName) ?? string.Empty,
                    EmailConfirmed = bool.Parse(principal.FindFirstValue(JwtClaimNames.EmailConfirmed) ?? "false"),
                };
            }
            catch
            {
                return null;
            }
        }

        private string GenerateToken(UserProfileDto userProfile, string secretKey, DateTime expires, DateTime? refreshTokenExpires)
        {
            var claims = new List<Claim>
            {
                new(JwtClaimNames.Id, userProfile.Id),
                new(JwtClaimNames.Email, userProfile.Email),
                new(JwtClaimNames.FirstName, userProfile.FirstName),
                new(JwtClaimNames.LastName, userProfile.LastName),
                new(JwtClaimNames.EmailConfirmed, userProfile.EmailConfirmed.ToString()),
                new("type", userProfile.EmailConfirmed ? TokenType.Login : TokenType.NeedActivate)
            };

            if (refreshTokenExpires.HasValue)
            {
                claims.Add(new Claim(JwtClaimNames.RefreshTokenExpires,
                    new DateTimeOffset(refreshTokenExpires.Value).ToUnixTimeMilliseconds().ToString()));
            }

            var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(_jwtConfig.Issuer, _jwtConfig.Audience, claims, null, expires,
                signingCredentials);

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
        public const string RefreshTokenExpires = "rte";
    }
}