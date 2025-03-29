namespace Lib.ExternalServices.KuCoin.Models
{
    public class PlaceOrderRequest
    {
        public string Side { get; init; } // buy or sell

        public string Symbol { get; init; } // e.g., BTC-USDT

        public string Type { get; init; } // limit or market

        public string Price { get; init; } // required for limit orders

        public string Size { get; init; } // required for both limit and market orders
    }

    public class PlaceOrderResponse
    {
        public string OrderId { get; set; }
    }
}