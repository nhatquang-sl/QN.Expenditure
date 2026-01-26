using System.ComponentModel;
using System.Reflection;

namespace Lib.ExternalServices.KuCoin.Models;

public enum IntervalType
{
    [Description("5min")] FiveMinutes,
    [Description("15min")] FifteenMinutes,
    [Description("30min")] ThirtyMinutes,
    [Description("1hour")] OneHour,
    [Description("4hour")] FourHours,
    [Description("1day")] OneDay
}

public static class IntervalTypeExtensions
{
    public static string GetDescription(this IntervalType intervalType)
    {
        var field = intervalType.GetType().GetField(intervalType.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? intervalType.ToString();
    }

    public static TimeSpan GetTimeSpan(this IntervalType intervalType)
    {
        return intervalType switch
        {
            IntervalType.FiveMinutes => TimeSpan.FromMinutes(5),
            IntervalType.FifteenMinutes => TimeSpan.FromMinutes(15),
            IntervalType.ThirtyMinutes => TimeSpan.FromMinutes(30),
            IntervalType.OneHour => TimeSpan.FromHours(1),
            IntervalType.FourHours => TimeSpan.FromHours(4),
            IntervalType.OneDay => TimeSpan.FromDays(1),
            _ => throw new ArgumentOutOfRangeException(nameof(intervalType),
                $"Unsupported interval type: {intervalType}")
        };
    }

    public static DateTime GetStartDate(this IntervalType intervalType, DateTime? currentDateTime = null)
    {
        var now = currentDateTime ?? DateTime.UtcNow;
        return intervalType switch
        {
            IntervalType.FiveMinutes => now.AddDays(-5),
            IntervalType.FifteenMinutes => now.AddDays(-15),
            IntervalType.ThirtyMinutes => now.AddDays(-30),
            IntervalType.OneHour => now.AddDays(-60),
            IntervalType.FourHours => now.AddDays(-120),
            IntervalType.OneDay => now.AddDays(-1440),
            _ => throw new ArgumentOutOfRangeException(nameof(intervalType),
                $"Unsupported interval type: {intervalType}")
        };
    }
}
