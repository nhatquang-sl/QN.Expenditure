using Lib.ExternalServices.Bnb.Models;
using Refit;
using System.Text.Json;

namespace Lib.ExternalServices.Bnb
{
    public interface IBnbService
    {
        [Get("/api/v3/time")]
        Task<ServerTimeResponse> GetServerTime();

        //https://binance-docs.github.io/apidocs/spot/en/#all-orders-user_data
        [Get("/api/v3/allOrders")]
        Task<List<SpotOrderRaw>> AllOrders([Header("X-MBX-APIKEY")] string authorization, AllOrdersRequest request);


        //https://binance-docs.github.io/apidocs/spot/en/#symbol-price-ticker
        [Get("/api/v3/ticker/price")]
        internal Task<List<TickerPriceRaw>> GetTickerPrice(string symbols);


        public async Task<List<TickerPrice>> GetTickerPrice(string[] symbols)
        {
            var data = await GetTickerPrice(JsonSerializer.Serialize(symbols));

            return data.Select(x => new TickerPrice
            {
                Symbol = x.Symbol,
                Price = decimal.Parse(x.Price)
            }).ToList();
        }

        [Get("/api/v3/ticker/bookTicker")]
        Task<List<OrderBookTickerRaw>> GetOrderBookTicker(string symbols);
    }

    public class TickerPrice
    {
        public string Symbol { get; set; }
        public decimal Price { get; set; }
    }

    internal class TickerPriceRaw
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
