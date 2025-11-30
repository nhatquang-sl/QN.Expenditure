using Cex.Domain.Enums;

namespace Cex.Domain.Entities
{
    public class ExchangeConfig
    {
        public string UserId { get; set; } = string.Empty;
        public ExchangeName ExchangeName { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string? Passphrase { get; set; }
    }
}
