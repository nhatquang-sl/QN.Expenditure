using Refit;

namespace Lib.ExternalServices.Bnd.Models
{
    public class ServerTimeResponse
    {
        [AliasAs("serverTime")]
        public long ServerTime { get; set; }
    }
}
