using Cex.Domain.Entities;
using Lib.ExternalServices.KuCoin;

namespace Cex.Application.Common.Extensions;

public static class ExchangeSettingExtensions
{
    public static KuCoinConfig ToKuCoinConfig(this ExchangeSetting exchangeSetting)
    {
        return new KuCoinConfig
        {
            ApiKey = exchangeSetting.ApiKey,
            ApiSecret = exchangeSetting.Secret,
            ApiPassphrase = exchangeSetting.Passphrase ?? string.Empty
        };
    }
}
