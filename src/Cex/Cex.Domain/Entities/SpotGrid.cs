namespace Cex.Domain.Entities
{
    public class SpotGrid
    {
        public long Id { get; set; }
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public decimal LowerPrice { get; set; }
        public decimal UpperPrice { get; set; }
        public decimal TriggerPrice { get; set; }
        public int NumberOfGrids { get; set; }
        public SpotGridMode GridMode { get; set; }
        public decimal Investment { get; set; }
        public decimal BaseBalance { get; set; } // USDT
        public decimal QuoteBalance { get; set; } // BTC, ETH, BNB, etc.
        public decimal Profit { get; set; }
        public decimal? TakeProfit { get; set; }
        public decimal? StopLoss { get; set; }
        public SpotGridStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ICollection<SpotGridStep> GridSteps { get; private set; } = new List<SpotGridStep>();
    }

    public enum SpotGridStatus
    {
        NEW,
        RUNNING,
        TAKE_PROFIT,
        STOP_LOSS,
        PAUSED
    }

    public enum SpotGridMode
    {
        ARITHMETIC,
        GEOMETRIC
    }
}