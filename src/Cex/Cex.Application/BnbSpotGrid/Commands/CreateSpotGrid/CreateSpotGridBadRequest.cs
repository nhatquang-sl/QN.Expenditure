namespace Cex.Application.BnbSpotGrid.Commands.CreateSpotGrid
{
    public class CreateSpotGridBadRequest
    {
        public string Symbol { get; set; }
        public string UpperPrice { get; set; }
        public string TakeProfit { get; set; }
    }
}