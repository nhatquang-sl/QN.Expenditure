using Cex.Domain.Entities;
using Lib.Application.Extensions;

namespace Cex.Application.Sync.SyncSetting.DTOs;

public class SyncSettingDto
{
    public string Symbol { get; set; } = string.Empty;
    public long StartSync { get; set; }
    public long LastSync { get; set; }

    public SyncSettingDto()
    {
    }

    public SyncSettingDto(Domain.Entities.SyncSetting syncSetting)
    {
        Symbol = syncSetting.Symbol;
        StartSync = syncSetting.StartSync.ToUnixTimestampMilliseconds();
        LastSync = syncSetting.LastSync.ToUnixTimestampMilliseconds();
    }
}
