namespace Application.Common.Abstractions
{
    public interface IJwtProvider
    {
        (string accessToken, string refreshToken) GenerateTokens(TokenParam tokenInfo);
    }

    public class TokenParam
    {
        public string Id { get; set; }
        public string EmailAddress { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string[] Roles { get; set; }
    }
}
