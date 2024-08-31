using Refit;
using System.Text.Json.Serialization;

namespace Cex.Application.Common.ExServices.Cex
{
    public interface ICexService
    {
        [Post("/api/auth/auth/token?refresh=1")]
        Task<CexServiceResponse<RefreshTokenResponseData>> RefreshToken(RefreshTokenRequest request);

        [Get("/api/wallet/binaryoption/prices")]
        internal Task<CexServiceResponse<List<List<decimal>>>> GetPricesAsync([Authorize("Bearer")] string accessToken);

        public async Task<List<Domain.Candle>> GetPrices(string accessToken)
        {
            var res = await GetPricesAsync(accessToken);
            if (!res.Ok) throw new CexServiceException(res.ErrorMessage);
            var prices = new List<Domain.Candle>();
            foreach (var item in res.Data)
            {
                var p = new Domain.Candle
                {
                    OpenDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)item[0]).UtcDateTime,
                    OpenPrice = item[1],
                    HighPrice = item[2],
                    LowPrice = item[3],
                    ClosePrice = item[4],
                    BaseVolume = item[5],
                    CloseDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)item[6]).UtcDateTime,
                    QuoteVolume = item[7],
                    IsBetSession = item[8] == 1,
                    Session = (long)item[9]
                };
                prices.Add(p);
            }
            return prices;
        }
    }

    public class CexServiceException(string message) : Exception(message) { }


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
