namespace Cex.Domain.Entities
{
    public class TradeHistory
    {
        public long UserId { get; set; }
        public string Symbol { get; set; }
        public long TradeId { get; set; }
        public string OrderId { get; set; }
        public string CounterOrderId { get; set; }
        public string Side { get; set; }
        public string Liquidity { get; set; }
        public bool ForceTaker { get; set; }
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public decimal Funds { get; set; }
        public decimal Fee { get; set; }
        public decimal FeeRate { get; set; }
        public string FeeCurrency { get; set; }
        public string Stop { get; set; }
        public string TradeType { get; set; }
        public string Type { get; set; }
        public DateTime TradedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}