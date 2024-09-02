using Lib.ExternalServices.Cex.Models;
using System.IdentityModel.Tokens.Jwt;

namespace Lib.ExternalServices.Cex
{
    public static class CexUtils
    {
        public static JwtTokenData DecodeToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var reToken = handler.ReadJwtToken(token);
            var exp = long.Parse(reToken.Claims.FirstOrDefault(x => x.Type == "exp")?.Value ?? "0");

            return new JwtTokenData
            {
                UserId = long.Parse(reToken.Claims.FirstOrDefault(x => x.Type == "uid")?.Value ?? "0"),
                Email = reToken.Claims.FirstOrDefault(x => x.Type == "email")?.Value ?? "",
                Nickname = reToken.Claims.FirstOrDefault(x => x.Type == "nick_name")?.Value ?? "",
                ExpiredAt = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime
            };
        }
    }
}
