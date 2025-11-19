namespace Lib.ExternalServices.KuCoin.Models
{
    public class TradeHistoryResponse
    {
        public List<TradeHistory> Items { get; set; }
    }

    public class TradeHistory
    {
        public string Symbol { get; set; }
        public string TradeId { get; set; }
        public string OrderId { get; set; }
        public string CounterOrderId { get; set; }
        public string Side { get; set; }
        public string Liquidity { get; set; }
        public bool ForceTaker { get; set; }
        public string Price { get; set; }
        public string Size { get; set; }
        public string Funds { get; set; }
        public string Fee { get; set; }
        public string FeeRate { get; set; }
        public string FeeCurrency { get; set; }
        public string Stop { get; set; }
        public string TradeType { get; set; }
        public string Type { get; set; }
        public long CreatedAt { get; set; }
    }
}