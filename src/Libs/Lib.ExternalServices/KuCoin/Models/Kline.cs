namespace Lib.ExternalServices.KuCoin.Models
{
    public class Kline
    {
        public DateTime OpenTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal Amount { get; set; }
    }
}