namespace Lib.ExternalServices.Cex.Models
{
    public class JwtTokenData
    {
        public long UserId { get; set; }
        public string Email { get; set; }
        public string Nickname { get; set; }
        public DateTime ExpiredAt { get; set; }
    }
}
