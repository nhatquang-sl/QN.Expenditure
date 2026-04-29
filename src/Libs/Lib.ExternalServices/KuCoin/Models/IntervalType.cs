using System.ComponentModel;
using System.Reflection;

namespace Lib.ExternalServices.KuCoin.Models;

public enum IntervalType
{
    [Description("1min")] OneMinute,
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

    public static DateTime GetStartDate(this IntervalType intervalType, DateTime? currentDateTime = null)
    {
        var now = currentDateTime ?? DateTime.UtcNow;
        return intervalType switch
        {
            IntervalType.OneMinute => now.AddMinutes(-1440),
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
    
    public static DateTime GetEndDate(this IntervalType intervalType, DateTime? currentDateTime = null)
    {
        var now = currentDateTime ?? DateTime.UtcNow;
        return intervalType switch
        {
            IntervalType.OneMinute => now.AddMinutes(1440),
            IntervalType.FiveMinutes => now.AddDays(5),
            IntervalType.FifteenMinutes => now.AddDays(15),
            IntervalType.ThirtyMinutes => now.AddDays(30),
            IntervalType.OneHour => now.AddDays(60),
            IntervalType.FourHours => now.AddDays(120),
            IntervalType.OneDay => now.AddDays(1440),
            _ => throw new ArgumentOutOfRangeException(nameof(intervalType),
                $"Unsupported interval type: {intervalType}")
        };
    }
}
