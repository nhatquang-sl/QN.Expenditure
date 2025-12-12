namespace Cex.Domain.Entities
{
    public class StatisticTrade
    {
        public decimal EntryPrice { get; set; }
        public DateTime DetectedTime { get; set; }
        public DateTime PreviousTime { get; set; }
        public decimal PreviousPrice { get; set; }
        public decimal MaxProfit { get; set; }
        public DateTime MaxProfitTime { get; set; }
        public decimal LiquidationPrice { get; set; }
        public DateTime LiquidationTime { get; set; }
        public string TradeType { get; set; } // LONG or SHORT
        public string TimeInterval { get; set; }
    }
}