using Cex.Domain.Entities;
using Cex.Domain.Enums;

namespace Cex.Application.ExchangeConfigs.DTOs
{
    public class ExchangeConfigDto
    {
        public ExchangeName ExchangeName { get; set; }
        public string ApiKey { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string? Passphrase { get; set; }

        public ExchangeConfigDto()
        {
        }

        public ExchangeConfigDto(ExchangeConfig exchangeConfig)
        {
            ExchangeName = exchangeConfig.ExchangeName;
            ApiKey = exchangeConfig.ApiKey;
            Secret = exchangeConfig.Secret;
            Passphrase = exchangeConfig.Passphrase;
        }
    }
}
