using Refit;

namespace Lib.ExternalServices.Bnb.Models
{
    public class ServerTimeResponse
    {
        [AliasAs("serverTime")]
        public long ServerTime { get; set; }
    }
}
