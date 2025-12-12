using System.Text.Json.Serialization;

namespace Lib.ExternalServices.Cex.Models
{
    public class RefreshTokenResponseData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
    }

    public class CexServiceResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("d")]
        public T Data { get; set; }

        [JsonPropertyName("m")]
        public string ErrorMessage { get; set; }
    }
}
