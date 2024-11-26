namespace Domain.Entities
{
    public class SpotGridOrder
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public decimal Price { get; set; }
        public decimal OrigQty { get; set; }
        public string Side { get; set; }
        public SpotGrid SpotGrid { get; set; }
    }
}
