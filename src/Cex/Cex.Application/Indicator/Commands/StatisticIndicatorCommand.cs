using System.Text.Json;
using Cex.Application.Indicator.Commands.Rsi;
using Cex.Application.Indicator.Shared;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Cex.Application.Indicator.Commands
{
    public class StatisticIndicatorCommand : IRequest
    {
    }

    public class StatisticDivergence(DivergenceResult divergence) : DivergenceResult(divergence.Type,
        divergence.EntryPrice, divergence.Time, divergence.Rsi, divergence.PreviousTime)
    {
        public DateTime? LiquidatedAt { get; set; }
        public decimal LiquidationPrice { get; set; }
        public decimal Profit { get; set; }
        public Kline ProfitCandle { get; set; }
        public DivergenceStatus Status { get; set; } = DivergenceStatus.Awaiting;
    }

    public enum DivergenceStatus
    {
        Awaiting,
        Ordered,
        StopLoss
    }

    public class StatisticIndicatorCommandHandler(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        ISender sender,
        INotifier notifier)
        : IRequestHandler<StatisticIndicatorCommand>
    {
        private const int Leverage = 10;

        public async Task Handle(StatisticIndicatorCommand command, CancellationToken cancellationToken)
        {
            var divergences = new List<StatisticDivergence>();
            const IntervalType intervalType = IntervalType.OneHour;
            // var fromAt = new DateTime(2024, 4, 20, 0, 1, 30, DateTimeKind.Utc); // 5 minutes
            var startAt = new DateTime(2025, 1, 1, 0, 1, 30, DateTimeKind.Utc); // 5 minutes
            startAt = new DateTime(2024, 6, 1, 0, 1, 30, DateTimeKind.Utc); // 5 minutes
            // var endAt = new DateTime(2025, 6, 20, 0, 1, 30, DateTimeKind.Utc);
            var from = startAt;
            var maxProfit = 0m;
            var allCandles = new List<Kline>();
            var batchSize = 1400;
            while (from <= DateTime.UtcNow)
            {
                var result = await kuCoinService.GetKlines("BTCUSDT", intervalType.GetDescription(),
                    from, from.AddHours(batchSize), kuCoinConfig.Value);
                allCandles.AddRange(result);
                from = from.AddHours(batchSize);
            }

            for (var i = 0; i < allCandles.Count - 1; i++)
            {
                var firstCandle = allCandles[i].OpenTime;
                if (firstCandle.AddHours(1) == allCandles[i + 1].OpenTime)
                {
                    continue;
                }

                throw new InvalidDataException();
            }

            var allRsiValues = await sender.Send(new RsiCommand(allCandles), cancellationToken);
            var pointer = 0;
            while (pointer < allCandles.Count - batchSize)
            {
                try
                {
                    var candles = allCandles.Skip(pointer).Take(batchSize).ToList();
                    var rsiValues = allRsiValues.Skip(pointer).Take(batchSize).ToDictionary();
                    var result = await sender.Send(new DivergenceCommand(candles, rsiValues), cancellationToken);
                    var divergence = new StatisticDivergence(result)
                    {
                        LiquidationPrice = (result.EntryPrice + 0.7m * result.EntryPrice / Leverage).FixedNumber(),
                        Profit = 0m
                    };

                    if (divergence.Type == DivergenceType.Peak)
                    {
                        var nextCandles = allCandles.Where(x => x.OpenTime > divergence.Time)
                            .OrderBy(x => x.OpenTime)
                            .Take(1000)
                            .ToList();
                        foreach (var candle in nextCandles)
                        {
                            switch (divergence.Status)
                            {
                                case DivergenceStatus.Awaiting:
                                    if (candle.HighestPrice > divergence.EntryPrice)
                                    {
                                        divergence.Status = DivergenceStatus.Ordered;
                                    }

                                    break;
                                case DivergenceStatus.Ordered:
                                    if (candle.HighestPrice > divergence.LiquidationPrice)
                                    {
                                        divergence.Status = DivergenceStatus.StopLoss;
                                        divergence.LiquidatedAt = candle.OpenTime;
                                    }
                                    else if (candle.LowestPrice < divergence.EntryPrice)
                                    {
                                        var profit =
                                            (100 * (divergence.EntryPrice - candle.LowestPrice) / divergence.EntryPrice)
                                            .FixedNumber(2) *
                                            Leverage;
                                        if (profit > divergence.Profit)
                                        {
                                            divergence.ProfitCandle = candle;
                                            divergence.Profit = profit;
                                        }
                                    }

                                    break;
                            }

                            if (divergence.Status == DivergenceStatus.StopLoss)
                            {
                                break;
                            }
                        }

                        divergences.Add(divergence);
                    }

                    pointer++;
                }
                catch (Exception ex)
                {
                }
            }

            // while (now <= DateTime.UtcNow)
            // {
            //     var candles = await kuCoinService.GetKlines("BTCUSDT", intervalType.GetDescription(),
            //         intervalType.GetStartDate(now), now, kuCoinConfig.Value);
            //
            //     var result = await sender.Send(new DivergenceCommand(candles), cancellationToken);
            //     var divergence = new StatisticDivergence(result)
            //     {
            //         LiquidationPrice = (result.EntryPrice + 0.7m * result.EntryPrice / Leverage).FixedNumber(),
            //         MaxProfit = 0m
            //     };
            //
            //     candles = await kuCoinService.GetKlines("BTCUSDT", intervalType.GetDescription(),
            //         now, now.AddHours(1000), kuCoinConfig.Value);
            //     if (divergence.Type == DivergenceType.Peak)
            //     {
            //         foreach (var candle in candles)
            //         {
            //             if (candle.HighestPrice >= divergence.LiquidationPrice)
            //             {
            //                 divergence.LiquidatedAt = candle.OpenTime;
            //                 break;
            //             }
            //
            //             if (candle.LowestPrice > divergence.EntryPrice)
            //             {
            //                 continue;
            //             }
            //
            //             var profit =
            //                 (100 * (divergence.EntryPrice - candle.LowestPrice) / divergence.EntryPrice)
            //                 .FixedNumber(2) *
            //                 Leverage;
            //
            //             if (profit <= divergence.MaxProfit)
            //             {
            //                 continue;
            //             }
            //
            //             divergence.MaxProfit = profit;
            //             divergence.MaxProfitCandle = candle;
            //
            //             maxProfit = Math.Max(maxProfit, profit);
            //         }
            //
            //         divergences.Add(divergence);
            //     }
            //
            //     now = now.AddHours(1);
            // }
            //
            var msg = JsonSerializer.Serialize(divergences);
            var stopLoss = divergences.Where(x => x.Status == DivergenceStatus.StopLoss).ToList();
            var minProfit = divergences.Min(x => x.Profit);
            // var now = new DateTime(2025, 6, 16, 16, 1, 30, DateTimeKind.Utc); // 1 hour
            // now = new DateTime(2025, 6, 16, 7, 51, 30, DateTimeKind.Utc); // 5 minutes
        }
    }
}