namespace Infrastructure.Identity
{
    public class JwtConfig
    {
        public string Issuer { get; init; }
        public string Audience { get; set; }
        public string AccessTokenSecretKey { get; set; }
        public string RefreshTokenSecretKey { get; set; }
    }
}
