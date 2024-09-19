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

        [Get("/api/v3/ticker/bookTicker")]
        Task<List<OrderBookTickerRaw>> GetOrderBookTicker(string symbols);
    }

    public class TickerPriceRaw
    {
        public string Symbol { get; set; }
        public string Price { get; set; }
    }

    public class OrderBookTickerRaw
    {
        public string Symbol { get; set; }
        public string BidPrice { get; set; } // buy
        public string BidQty { get; set; }
        public string AskPrice { get; set; } // sell
        public string AskQty { get; set; }
    }
}
