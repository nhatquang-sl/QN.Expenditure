using Cex.Domain.Entities;
using Cex.Domain.Enums;

namespace Cex.Application.Settings.ExchangeSetting.DTOs;

public class ExchangeSettingDto
{
    public ExchangeName ExchangeName { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string? Passphrase { get; set; }

    public ExchangeSettingDto()
    {
    }

    public ExchangeSettingDto(Domain.Entities.ExchangeSetting exchangeSetting)
    {
        ExchangeName = exchangeSetting.ExchangeName;
        ApiKey = exchangeSetting.ApiKey;
        Secret = exchangeSetting.Secret;
        Passphrase = exchangeSetting.Passphrase;
    }
}
