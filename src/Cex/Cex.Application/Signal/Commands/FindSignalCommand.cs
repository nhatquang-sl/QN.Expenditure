using System.Text;
using Cex.Application.Common.Abstractions;
using Cex.Application.Signal.Commands.Rsi;
using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Signal.Commands
{
    public class FindSignalCommand(IntervalType interval) : IRequest
    {
        public IntervalType Type { get; set; } = interval;
    }

    public class FindSignalCommandHandler(
        IKuCoinService kuCoinService,
        IOptions<KuCoinConfig> kuCoinConfig,
        ISender sender,
        INotifier notifier,
        ILogTrace logTrace,
        ICexDbContext dbContext)
        : IRequestHandler<FindSignalCommand>
    {
        public async Task Handle(FindSignalCommand command, CancellationToken cancellationToken)
        {
            logTrace.LogInformation(command.Type.GetDescription());
            var candles = await kuCoinService.GetKlines("BTCUSDT", command.Type,
                command.Type.GetStartDate(), DateTime.UtcNow, //.AddHours(-1).AddMinutes(-15),
                kuCoinConfig.Value);
            var rsiValues = await sender.Send(new RsiCommand(candles), cancellationToken);
            var div = await sender.Send(new DivergenceCommand(candles, rsiValues), cancellationToken);
            var divTime = div.Time.ToSimple();
            var divPreTime = div.PreviousTime.ToSimple();
            switch (div.Type)
            {
                case DivergenceType.Peak:
                    {
                        var dCandle = candles.First(x => x.OpenTime == div.Time);
                        var preCandle = candles.First(x => x.OpenTime == div.PreviousTime);
                        var entryPrice = candles[^1].ClosePrice;
                        var stopLoss = entryPrice * 1.08m;
                        var takeProfit = entryPrice * 0.92m;

                        var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Short</b> detected:\n");
                        msg.AppendLine($"[{divTime}]: <b>{div.Rsi} - {dCandle.HighestPrice}</b>");
                        msg.AppendLine($"[{divPreTime}]: <b>{rsiValues[div.PreviousTime]} - {preCandle.HighestPrice}</b>");
                        msg.AppendLine($"Entry price: <b>{entryPrice}</b>");
                        msg.AppendLine($"Liquidation 8x10: <b>{stopLoss.FixedNumber(2)}</b>");
                        await notifier.Notify(msg.ToString(), cancellationToken);

                        await SaveSignalRecordIfNewAsync(command, div, SignalType.Short, entryPrice, stopLoss, takeProfit,
                            rsiValues[div.PreviousTime], cancellationToken);
                        break;
                    }
                case DivergenceType.Trough:
                    {
                        var dCandle = candles.First(x => x.OpenTime == div.Time);
                        var preCandle = candles.First(x => x.OpenTime == div.PreviousTime);
                        var entryPrice = candles[^1].OpenPrice;
                        var stopLoss = entryPrice * 0.92m;
                        var takeProfit = entryPrice * 1.08m;

                        var msg = new StringBuilder($"[{command.Type.GetDescription()}] RSI <b>Long</b> detected:\n");
                        msg.AppendLine($"[{divTime}]: <b>{div.Rsi} - {dCandle.LowestPrice}</b>");
                        msg.AppendLine($"[{divPreTime}]: <b>{rsiValues[div.PreviousTime]} - {preCandle.LowestPrice}</b>");
                        msg.AppendLine($"Entry price: <b>{entryPrice}</b>");
                        msg.AppendLine($"Liquidation 8x10: <b>{stopLoss.FixedNumber(2)}</b>");
                        await notifier.Notify(msg.ToString(), cancellationToken);

                        await SaveSignalRecordIfNewAsync(command, div, SignalType.Long, entryPrice, stopLoss, takeProfit,
                            rsiValues[div.PreviousTime], cancellationToken);
                        break;
                    }
                case DivergenceType.None:
                default:
                    return;
            }
        }

        private async Task SaveSignalRecordIfNewAsync(
            FindSignalCommand command,
            DivergenceResult div,
            SignalType signalType,
            decimal entryPrice,
            decimal stopLoss,
            decimal takeProfit,
            decimal previousRsiValue,
            CancellationToken cancellationToken)
        {
            try
            {
                var interval = command.Type.GetDescription();

                await dbContext.SignalRecords.AddAsync(new SignalRecord
                {
                    Symbol = "BTCUSDT",
                    Interval = interval,
                    SignalType = signalType,
                    DetectedAt = div.Time,
                    PreviousCandleAt = div.PreviousTime,
                    RsiValue = div.Rsi,
                    PreviousRsiValue = previousRsiValue,
                    EntryPrice = entryPrice,
                    StopLoss = stopLoss,
                    TakeProfit = takeProfit,
                    LastCheckedCandleAt = DateTime.UtcNow,
                }, cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                logTrace.LogError("SaveSignalRecordIfNewAsync()", ex);
            }
        }

    }
}
