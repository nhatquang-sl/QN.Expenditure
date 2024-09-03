using AutoMapper;
using Cex.Application.Common.Abstractions;
using Lib.ExternalServices.Cex;
using Lib.ExternalServices.Telegram;
using Lib.ExternalServices.Telegram.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cex.Application.Candle.Commands.SyncCandles
{
    public record SyncCandlesCommand(string AccessToken) : IRequest { }

    public class SyncCandlesCommandHandler(ICexDbContext cexDbContext, ICexService cexService, IMapper mapper
    , ITelegramService telegramService, IOptions<TelegramServiceConfig> telegramConfig)
    : IRequestHandler<SyncCandlesCommand>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICexService _cexService = cexService;
        private readonly IMapper _mapper = mapper;
        private readonly ITelegramService _telegramService = telegramService;
        private readonly TelegramServiceConfig _telegramConfig = telegramConfig.Value;

        public async Task Handle(SyncCandlesCommand command, CancellationToken cancellationToken)
        {
            var cexPrices = await _cexService.GetPrices(command.AccessToken);
            var minSession = cexPrices.Min(x => x.Session);
            var rmCandles = await _cexDbContext.Candles.Where(x => x.Session >= minSession).ToListAsync(cancellationToken);
            _cexDbContext.Candles.RemoveRange(rmCandles);

            var candles = _mapper.Map<List<Domain.Candle>>(cexPrices);
            _cexDbContext.Candles.AddRange(candles);

            await Task.WhenAll(
                _cexDbContext.SaveChangesAsync(cancellationToken),
                NotifyStreak(candles));
        }

        async Task NotifyStreak(List<Domain.Candle> candles)
        {
            var resultCandles = candles.Where(x => !x.IsBetSession).OrderByDescending(x => x.Session).ToList();
            var lastResult = resultCandles.First().ClosePrice - resultCandles.First().OpenPrice > 0 ? "UP" : "DOWN";
            var countMatch = 1;
            for (var i = 1; i < resultCandles.Count; i++)
            {
                var curResult = resultCandles[i].ClosePrice - resultCandles[i].OpenPrice > 0 ? "UP" : "DOWN";
                if (curResult != lastResult) break;
                countMatch++;
            }

            if (countMatch >= 5)
            {
                await _telegramService.SendMessage(_telegramConfig.BotToken
                    , new TelegramMessage(_telegramConfig.ChatId, $"Last {countMatch} are: <b>{lastResult}</b>"));
            }
        }
    }
}
