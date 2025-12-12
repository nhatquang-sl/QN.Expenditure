using System.ComponentModel;

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

    public class FPlaceOrderRequest
    {
        public string ClientOid { get; set; }
        public OrderSide Side { get; set; }
        public string Symbol { get; set; }
        public string Leverage { get; set; }
        public OrderType Type { get; set; }

        public string Price { get; set; }
        public int Size { get; set; }
    }


    public enum OrderStatus
    {
        [Description("active")] Active,
        [Description("done")] Done
    }

    public enum OrderType
    {
        [Description("limit")] Limit,
        [Description("market")] Market
    }

    public enum OrderSide
    {
        [Description("buy")] Buy,
        [Description("sell")] Sell
    }
}