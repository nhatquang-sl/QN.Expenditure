using Refit;

namespace Application.Common.ExServices.Bnb.Models
{
    public class ServerTimeResponse
    {
        [AliasAs("serverTime")]
        public long ServerTime { get; set; }
    }
}
