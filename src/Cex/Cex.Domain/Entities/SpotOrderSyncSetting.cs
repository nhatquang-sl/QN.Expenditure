namespace Cex.Domain.Entities
{
    public class SpotOrderSyncSetting
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public DateTime LastSyncAt { get; set; }
    }
}