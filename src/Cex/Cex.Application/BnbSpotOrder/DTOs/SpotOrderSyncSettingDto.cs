using Cex.Domain.Entities;
using Lib.Application.Extensions;

namespace Cex.Application.BnbSpotOrder.DTOs
{
    public class SpotOrderSyncSettingDto
    {
        public string Symbol { get; set; }
        public long LastSyncAt { get; set; }

        public static SpotOrderSyncSettingDto From(SpotOrderSyncSetting entity) => new()
        {
            Symbol = entity.Symbol,
            LastSyncAt = entity.LastSyncAt.ToUnixTimestampMilliseconds()
        };
    }

    public class SpotOrderSyncSettingUpdateDto
    {
        public long LastSyncAt { get; set; }
    }
}
