using System.Text.Json.Serialization;

namespace Lib.ExternalServices.Cex.Models
{
    public class CaptchaGeetest
    {
        [JsonPropertyName("captcha_output")]
        public string CaptchaOutput { get; set; } = "";

        [JsonPropertyName("gen_time")]
        public string GenTime { get; set; } = "";

        [JsonPropertyName("lot_number")]
        public string LotNumber { get; set; } = "";

        [JsonPropertyName("pass_token")]
        public string PassToken { get; set; } = "";
    }

    public class RefreshTokenRequest(string clientId, string refreshToken)
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = clientId;

        [JsonPropertyName("grant_type")]
        public string GrantType { get; set; } = "refresh_token";

        [JsonPropertyName("captcha")]
        public string Captcha { get; set; } = "string";

        [JsonPropertyName("captcha_geetest")]
        public CaptchaGeetest CaptchaGeetest { get; set; } = new CaptchaGeetest();

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = refreshToken;
    }
}
