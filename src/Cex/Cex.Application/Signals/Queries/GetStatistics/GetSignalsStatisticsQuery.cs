using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Signals.Queries.GetStatistics;

public record SignalStatisticInfo
{
    public int TotalSignals { get; init; }
    public int TotalEntries { get; init; }
    public int TotalMaxProfitHits { get; init; }
    public int TotalStopLossHits { get; init; }
    public decimal AvgEntryPrice { get; init; }
    public decimal AvgMaxProfit { get; init; }
}

public record SignalStatistics
{
    public SignalStatisticInfo Today { get; init; } = new();
    public SignalStatisticInfo Yesterday { get; init; } = new();
    public SignalStatisticInfo ThisWeek { get; init; } = new();
    public SignalStatisticInfo LastWeek { get; init; } = new();
    public SignalStatisticInfo ThisMonth { get; init; } = new();
    public SignalStatisticInfo LastMonth { get; init; } = new();
}

public record GetSignalsStatisticsQuery(
    string? Interval = null,
    SignalType? SignalType = null,
    DateTime? AsOf = null) : IRequest<SignalStatistics>;

public class GetSignalsStatisticsQueryHandler(ICexDbContext dbContext)
    : IRequestHandler<GetSignalsStatisticsQuery, SignalStatistics>
{
    private sealed record SignalRow(
        DateTime DetectedAt,
        DateTime? EntryHitAt,
        DateTime? MaxProfitHitAt,
        DateTime? StopLossHitAt,
        decimal EntryPrice,
        decimal MaxProfit);

    public async Task<SignalStatistics> Handle(
        GetSignalsStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var now = request.AsOf ?? DateTime.UtcNow;
        var periods = ComputePeriods(now);

        var query = dbContext.Signals
            .AsNoTracking()
            .Where(s => s.DetectedAt >= periods.LastMonthStart && s.DetectedAt <= now);

        if (!string.IsNullOrEmpty(request.Interval))
            query = query.Where(s => s.Interval == request.Interval);

        if (request.SignalType.HasValue)
            query = query.Where(s => s.SignalType == request.SignalType.Value);

        var raw = await query
            .Select(s => new
            {
                s.DetectedAt,
                s.EntryHitAt,
                s.MaxProfitHitAt,
                s.StopLossHitAt,
                s.EntryPrice,
                s.MaxProfit,
            })
            .ToListAsync(cancellationToken);

        var signals = raw
            .Select(s => new SignalRow(
                s.DetectedAt,
                s.EntryHitAt,
                s.MaxProfitHitAt,
                s.StopLossHitAt,
                s.EntryPrice,
                s.MaxProfit))
            .ToList();

        return new SignalStatistics
        {
            Today = Aggregate(signals, periods.TodayStart, periods.TodayEnd),
            Yesterday = Aggregate(signals, periods.YesterdayStart, periods.YesterdayEnd),
            ThisWeek = Aggregate(signals, periods.ThisWeekStart, periods.ThisWeekEnd),
            LastWeek = Aggregate(signals, periods.LastWeekStart, periods.LastWeekEnd),
            ThisMonth = Aggregate(signals, periods.ThisMonthStart, periods.ThisMonthEnd),
            LastMonth = Aggregate(signals, periods.LastMonthStart, periods.LastMonthEnd),
        };
    }

    private static SignalStatisticInfo Aggregate(List<SignalRow> signals, DateTime start, DateTime end)
    {
        var inPeriod = signals.Where(s => s.DetectedAt >= start && s.DetectedAt < end).ToList();

        if (inPeriod.Count == 0)
            return new SignalStatisticInfo();

        var entryHit = inPeriod.Where(s => s.EntryHitAt != null).ToList();
        var maxProfitHit = inPeriod.Where(s => s.MaxProfitHitAt != null).ToList();
        var stopLossHit = inPeriod.Where(s => s.StopLossHitAt != null).ToList();

        return new SignalStatisticInfo
        {
            TotalSignals = inPeriod.Count,
            TotalEntries = entryHit.Count,
            TotalMaxProfitHits = maxProfitHit.Count,
            TotalStopLossHits = stopLossHit.Count,
            AvgEntryPrice = entryHit.Count > 0 ? entryHit.Average(s => s.EntryPrice) : 0m,
            AvgMaxProfit = maxProfitHit.Count > 0 ? maxProfitHit.Average(s => s.MaxProfit) : 0m,
        };
    }

    private static Periods ComputePeriods(DateTime now)
    {
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

        // ISO 8601: week starts on Monday (DayOfWeek.Sunday == 0)
        var dayOfWeek = (int)now.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var thisWeekStart = todayStart.AddDays(-daysFromMonday);

        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        return new Periods(
            TodayStart: todayStart,
            TodayEnd: now,
            YesterdayStart: todayStart.AddDays(-1),
            YesterdayEnd: todayStart,
            ThisWeekStart: thisWeekStart,
            ThisWeekEnd: now,
            LastWeekStart: thisWeekStart.AddDays(-7),
            LastWeekEnd: thisWeekStart,
            ThisMonthStart: thisMonthStart,
            ThisMonthEnd: now,
            LastMonthStart: lastMonthStart,
            LastMonthEnd: thisMonthStart
        );
    }

    private sealed record Periods(
        DateTime TodayStart, DateTime TodayEnd,
        DateTime YesterdayStart, DateTime YesterdayEnd,
        DateTime ThisWeekStart, DateTime ThisWeekEnd,
        DateTime LastWeekStart, DateTime LastWeekEnd,
        DateTime ThisMonthStart, DateTime ThisMonthEnd,
        DateTime LastMonthStart, DateTime LastMonthEnd);
}
