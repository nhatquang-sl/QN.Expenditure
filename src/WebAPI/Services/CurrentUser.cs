using System.Security.Claims;
using Auth.Infrastructure.Identity;
using Lib.Application.Abstractions;

namespace WebAPI.Services
{
    public class CurrentUser : ICurrentUser
    {
        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            if (!(httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false))
            {
                return;
            }

            var claims = httpContextAccessor.HttpContext.User;
            Id = claims.FindFirstValue(JwtClaimNames.Id) ?? string.Empty;
            Email = claims.FindFirstValue(JwtClaimNames.Email) ?? string.Empty;
            FirstName = claims.FindFirstValue(JwtClaimNames.FirstName) ?? string.Empty;
            LastName = claims.FindFirstValue(JwtClaimNames.LastName) ?? string.Empty;
            EmailConfirmed = bool.Parse(claims.FindFirstValue(JwtClaimNames.EmailConfirmed) ?? false.ToString());
            AccessTokenExpires = new DateTimeOffset(claims.Identity is System.Security.Claims.ClaimsIdentity identity
                ? identity.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp) is { } expClaim
                    && long.TryParse(expClaim.Value, out var expSeconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime
                    : DateTime.UtcNow
                : DateTime.UtcNow).ToUnixTimeMilliseconds();
            RefreshTokenExpires = long.TryParse(claims.FindFirstValue(JwtClaimNames.RefreshTokenExpires), out var rte) ? rte : 0;
        }

        public string Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool EmailConfirmed { get; set; }
        public long AccessTokenExpires { get; set; }
        public long RefreshTokenExpires { get; set; }
    }
}