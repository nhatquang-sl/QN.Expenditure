using Lib.ExternalServices.Cex.Models;
using Refit;

namespace Lib.ExternalServices.Cex
{
    public interface ICexService
    {
        [Post("/api/auth/auth/token?refresh=1")]
        Task<CexServiceResponse<RefreshTokenResponseData>> RefreshToken(RefreshTokenRequest request);

        [Get("/api/wallet/binaryoption/prices")]
        internal Task<CexServiceResponse<List<List<decimal>>>> GetPricesAsync([Authorize("Bearer")] string accessToken);

        public async Task<List<CandlePrice>> GetPrices(string accessToken)
        {
            var res = await GetPricesAsync(accessToken);
            if (!res.Ok) throw new CexServiceException(res.ErrorMessage);
            var prices = new List<CandlePrice>();
            foreach (var item in res.Data)
            {
                var p = new CandlePrice
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
}
