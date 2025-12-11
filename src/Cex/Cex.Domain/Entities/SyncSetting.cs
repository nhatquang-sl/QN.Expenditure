namespace Cex.Domain.Entities;

public class SyncSetting
{
    public string UserId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public DateTime StartSync { get; set; }
    public DateTime LastSync { get; set; }
}
