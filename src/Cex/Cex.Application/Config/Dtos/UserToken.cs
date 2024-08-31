using System.Text.Json.Serialization;

namespace Cex.Application.Config.Dtos
{
    public class UserToken
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
    }
}
