using Cex.Application.Signals.Queries.GetStatistics;
using Cex.Domain.Enums;
using Cex.Infrastructure.Data;
using MediatR;
using Shouldly;
using SignalEntity = Cex.Domain.Entities.Signal;

namespace Cex.Infrastructure.IntegrationTests.Signal;

/// <summary>
/// Fixed point in time used for all tests: Wednesday, June 4 2025 10:00:00 UTC.
///
/// Period boundaries:
///   Today:      [2025-06-04 00:00, 2025-06-04 10:00)
///   Yesterday:  [2025-06-03 00:00, 2025-06-04 00:00)
///   ThisWeek:   [2025-06-02 00:00, 2025-06-04 10:00)  (Mon=Jun 2)
///   LastWeek:   [2025-05-26 00:00, 2025-06-02 00:00)
///   ThisMonth:  [2025-06-01 00:00, 2025-06-04 10:00)
///   LastMonth:  [2025-05-01 00:00, 2025-06-01 00:00)
///   Window:     [2025-05-01 00:00, 2025-06-04 10:00]
/// </summary>
public class GetSignalsStatisticsTests : DependencyInjectionFixture
{
    private static readonly DateTime AsOf = new(2025, 6, 4, 10, 0, 0, DateTimeKind.Utc);

    private readonly ISender _sender;

    public GetSignalsStatisticsTests()
    {
        _sender = GetService<ISender>();

        var db = GetService<CexDbContext>();
        db.Signals.AddRange(BuildSignals());
        db.SaveChanges();
    }

    // ── Period coverage ──────────────────────────────────────────────────────

    [Fact]
    public async Task Today_counts_signals_detected_on_current_utc_day()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        result.Today.TotalSignals.ShouldBe(2);
    }

    [Fact]
    public async Task Yesterday_counts_signals_detected_on_the_previous_utc_day()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        result.Yesterday.TotalSignals.ShouldBe(1);
    }

    [Fact]
    public async Task ThisWeek_counts_signals_from_monday_to_asOf()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Jun 2 (Mon) + Jun 3 (Tue) + Jun 4 (Wed) signals = 1 + 1 + 2 = 4
        result.ThisWeek.TotalSignals.ShouldBe(4);
    }

    [Fact]
    public async Task LastWeek_counts_signals_from_prev_monday_to_prev_sunday()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // [May 26 00:00, Jun 2 00:00): May 26, May 30, Jun 1 = 3
        result.LastWeek.TotalSignals.ShouldBe(3);
    }

    [Fact]
    public async Task ThisMonth_counts_signals_from_month_start_to_asOf()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Jun 1, 2, 3, 4 signals = 1 + 1 + 1 + 2 = 5
        result.ThisMonth.TotalSignals.ShouldBe(5);
    }

    [Fact]
    public async Task LastMonth_counts_signals_from_previous_month()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // May signals in window: May 1, May 26, May 30 = 3
        result.LastMonth.TotalSignals.ShouldBe(3);
    }

    [Fact]
    public async Task Signals_before_window_are_excluded()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Apr 30 signal is before lastMonthStart (May 1) — should not appear anywhere
        var total = result.Today.TotalSignals + result.Yesterday.TotalSignals +
                    result.ThisWeek.TotalSignals + result.LastWeek.TotalSignals +
                    result.ThisMonth.TotalSignals + result.LastMonth.TotalSignals;
        // 2 + 1 + 4 + 1 + 5 + 3 = 16 (counting signals appearing in multiple periods)
        // Just verify last month doesn't include the pre-window signal
        result.LastMonth.TotalSignals.ShouldBe(3);
    }

    // ── Hit counts ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TotalEntries_counts_only_signals_with_EntryHitAt_set()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Today: 2 signals, 1 has EntryHitAt set
        result.Today.TotalEntries.ShouldBe(1);
    }

    [Fact]
    public async Task TotalMaxProfitHits_counts_only_signals_with_MaxProfitHitAt_set()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Today: 2 signals, 1 has MaxProfitHitAt set
        result.Today.TotalMaxProfitHits.ShouldBe(1);
    }

    [Fact]
    public async Task TotalStopLossHits_counts_only_signals_with_StopLossHitAt_set()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Today: 2 signals, 1 has StopLossHitAt set
        result.Today.TotalStopLossHits.ShouldBe(1);
    }

    // ── Averages ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AvgEntryPrice_averages_EntryPrice_over_entry_hit_signals_only()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Today: only signal-A has EntryHitAt; its EntryPrice = 50000
        result.Today.AvgEntryPrice.ShouldBe(50_000m);
    }

    [Fact]
    public async Task AvgMaxProfit_averages_MaxProfit_over_max_profit_hit_signals_only()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Today: only signal-A has MaxProfitHitAt; its MaxProfit = 5.5
        result.Today.AvgMaxProfit.ShouldBe(5.5m);
    }

    [Fact]
    public async Task AvgEntryPrice_is_zero_when_no_signals_hit_entry()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // Yesterday: 1 signal with no EntryHitAt
        result.Yesterday.AvgEntryPrice.ShouldBe(0m);
    }

    [Fact]
    public async Task AvgMaxProfit_is_zero_when_no_signals_hit_max_profit()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        result.Yesterday.AvgMaxProfit.ShouldBe(0m);
    }

    // ── Filters ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Interval_filter_excludes_signals_of_other_intervals()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(Interval: "1hour", AsOf: AsOf));
        // Today: signal-A is "1hour", signal-B is "15min" → only signal-A matches
        result.Today.TotalSignals.ShouldBe(1);
    }

    [Fact]
    public async Task SignalType_filter_excludes_signals_of_other_types()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(SignalType: SignalType.Short, AsOf: AsOf));
        // Today: signal-A is Long, signal-B is Short → only signal-B matches
        result.Today.TotalSignals.ShouldBe(1);
    }

    [Fact]
    public async Task Zero_period_returns_all_zero_SignalStatisticInfo()
    {
        // Query for a non-existent interval to get empty results
        var result = await _sender.Send(new GetSignalsStatisticsQuery(Interval: "99min", AsOf: AsOf));
        result.Today.TotalSignals.ShouldBe(0);
        result.Today.TotalEntries.ShouldBe(0);
        result.Today.TotalMaxProfitHits.ShouldBe(0);
        result.Today.TotalStopLossHits.ShouldBe(0);
        result.Today.AvgEntryPrice.ShouldBe(0m);
        result.Today.AvgMaxProfit.ShouldBe(0m);
    }

    // ── Week boundary ────────────────────────────────────────────────────────

    [Fact]
    public async Task LastWeek_signal_on_Monday_boundary_is_included_in_last_week_not_this_week()
    {
        var result = await _sender.Send(new GetSignalsStatisticsQuery(AsOf: AsOf));
        // May 26 is the start of LastWeek; Jun 2 is the start of ThisWeek
        // Signal at May 26 00:00:00 is in LastWeek (>= May 26, < Jun 2)
        // [May 26, Jun 2): May 26, May 30, Jun 1 (Jun 1 < Jun 2 00:00)
        result.LastWeek.TotalSignals.ShouldBe(3);
        // [Jun 2 00:00, asOf): Jun 2 08:00, Jun 3, Jun 4×2 = 4
        result.ThisWeek.TotalSignals.ShouldBe(4);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static List<SignalEntity> BuildSignals()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc); // CreatedAt placeholder

        return
        [
            // ── Before window (should be excluded) ──
            MakeSignal(new DateTime(2025, 4, 30, 12, 0, 0, DateTimeKind.Utc), "1hour", SignalType.Long,
                entryPrice: 45_000m, maxProfit: 0m, createdAt: now),

            // ── LastMonth: May 2025 ──
            MakeSignal(new DateTime(2025, 5, 1, 8, 0, 0, DateTimeKind.Utc), "1hour", SignalType.Long,
                entryPrice: 48_000m, maxProfit: 0m, createdAt: now),
            MakeSignal(new DateTime(2025, 5, 26, 0, 0, 0, DateTimeKind.Utc), "5min", SignalType.Short,
                entryPrice: 49_000m, maxProfit: 0m, createdAt: now), // LastWeek start boundary
            MakeSignal(new DateTime(2025, 5, 30, 14, 0, 0, DateTimeKind.Utc), "15min", SignalType.Long,
                entryPrice: 47_000m, maxProfit: 0m, createdAt: now),

            // ── ThisMonth + LastWeek, Jun 1 (before thisWeekStart Jun 2) ──
            MakeSignal(new DateTime(2025, 6, 1, 9, 0, 0, DateTimeKind.Utc), "1hour", SignalType.Long,
                entryPrice: 51_000m, maxProfit: 0m, createdAt: now),

            // ── ThisMonth, ThisWeek, Jun 2 (thisWeekStart) ──
            MakeSignal(new DateTime(2025, 6, 2, 8, 0, 0, DateTimeKind.Utc), "5min", SignalType.Long,
                entryPrice: 52_000m, maxProfit: 0m, createdAt: now),

            // ── Yesterday (Jun 3) ──
            MakeSignal(new DateTime(2025, 6, 3, 15, 0, 0, DateTimeKind.Utc), "1hour", SignalType.Short,
                entryPrice: 53_000m, maxProfit: 0m, createdAt: now),

            // ── Today (Jun 4): signal-A (Long, 1hour, entry+maxProfit+stopLoss all hit) ──
            MakeSignal(new DateTime(2025, 6, 4, 5, 0, 0, DateTimeKind.Utc), "1hour", SignalType.Long,
                entryPrice: 50_000m, maxProfit: 5.5m, createdAt: now,
                entryHitAt: new DateTime(2025, 6, 4, 6, 0, 0, DateTimeKind.Utc),
                maxProfitHitAt: new DateTime(2025, 6, 4, 7, 0, 0, DateTimeKind.Utc),
                stopLossHitAt: new DateTime(2025, 6, 4, 8, 0, 0, DateTimeKind.Utc)),

            // ── Today (Jun 4): signal-B (Short, 15min, no hits) ──
            MakeSignal(new DateTime(2025, 6, 4, 9, 0, 0, DateTimeKind.Utc), "15min", SignalType.Short,
                entryPrice: 60_000m, maxProfit: 0m, createdAt: now),
        ];
    }

    private static SignalEntity MakeSignal(
        DateTime detectedAt,
        string interval,
        SignalType signalType,
        decimal entryPrice,
        decimal maxProfit,
        DateTime createdAt,
        DateTime? entryHitAt = null,
        DateTime? maxProfitHitAt = null,
        DateTime? stopLossHitAt = null)
    {
        return new SignalEntity
        {
            Symbol = "BTCUSDT",
            Interval = interval,
            SignalType = signalType,
            DetectedAt = detectedAt,
            PreviousCandleAt = detectedAt.AddMinutes(-5),
            RsiValue = 30m,
            PreviousRsiValue = 28m,
            EntryPrice = entryPrice,
            StopLoss = entryPrice * 0.97m,
            TakeProfit = entryPrice * 1.05m,
            Leverage = 10,
            MaxProfit = maxProfit,
            EntryHitAt = entryHitAt,
            MaxProfitHitAt = maxProfitHitAt,
            StopLossHitAt = stopLossHitAt,
            CreatedAt = createdAt,
            LastCheckedCandleAt = detectedAt,
        };
    }
}
