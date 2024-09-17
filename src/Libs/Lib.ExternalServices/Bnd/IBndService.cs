using Lib.ExternalServices.Bnd.Models;
using Refit;

namespace Lib.ExternalServices.Bnd
{
    public interface IBndService
    {
        [Get("/api/v3/time")]
        Task<ServerTimeResponse> GetServerTime();

        //https://binance-docs.github.io/apidocs/spot/en/#all-orders-user_data
        [Get("/api/v3/allOrders")]
        Task<List<SpotOrderRaw>> AllOrders([Header("X-MBX-APIKEY")] string authorization, AllOrdersRequest request);


        [Get("/api/v3/ticker/price")]
        Task<List<TickerPriceRaw>> GetTickerPrice(string symbols);
    }

    public class TickerPriceRaw
    {
        public string Symbol { get; set; }
        public string Price { get; set; }
    }
}
