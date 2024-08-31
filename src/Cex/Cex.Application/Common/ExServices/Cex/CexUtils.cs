using System.IdentityModel.Tokens.Jwt;

namespace Cex.Application.Common.ExServices.Cex
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

    public class JwtTokenData
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        public string Nickname { get; set; }
        public DateTime ExpiredAt { get; set; }
    }
}
