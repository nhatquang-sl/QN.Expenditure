using Application.Common.Logging;
using Lib.ExternalServices.Bnd;
using Lib.ExternalServices.Telegram;
using Lib.ExternalServices.Telegram.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Application.BnbSpotOrder.Commands.Arbitrage
{
    public class ArbitrageCommand : IRequest { }

    public class ArbitrageCommandHandler(IBndService bndService, ITelegramService telegramService
        , IOptions<TelegramServiceConfig> telegramConfig, ILogTrace logTrace)
        : IRequestHandler<ArbitrageCommand>
    {
        private readonly ILogTrace _logTrace = logTrace;
        private readonly IBndService _bndService = bndService;
        private readonly ITelegramService _telegramService = telegramService;
        private readonly TelegramServiceConfig _telegramConfig = telegramConfig.Value;
        private const decimal POW6 = 1000000;
        private const decimal POW4 = 10000;

        public async Task Handle(ArbitrageCommand request, CancellationToken cancellationToken)
        {
            var BASE_TOKEN = "USDT";
            var bridgeToken = "BTC";
            var mainTokens = new List<string> { "1INCH", "AAVE", "ACA", "ACE", "ACH", "ADA", "ADX", "FTM", "TIA", "DYM", "SOL", "BNB" };
            //mainTokens = ["ACH"];

            var tasks = mainTokens.Select(async mainToken =>
            {
                var tokenPrices = await _bndService.GetOrderBookTicker($"[\"{mainToken}{BASE_TOKEN}\",\"{mainToken}{bridgeToken}\",\"{bridgeToken}{BASE_TOKEN}\"]");
                var mainPrice = tokenPrices.First(x => x.Symbol == $"{mainToken}{BASE_TOKEN}");
                var mainToBridgePrice = tokenPrices.First(x => x.Symbol == $"{mainToken}{bridgeToken}");
                var bridgePrice = tokenPrices.First(x => x.Symbol == $"{bridgeToken}{BASE_TOKEN}");

                await CheckArbitrage([BASE_TOKEN, mainToken, bridgeToken], [mainPrice, bridgePrice, mainToBridgePrice]);
            });

            await Task.WhenAll(tasks);
        }

        async Task CheckArbitrage(List<string> route, List<OrderBookTickerRaw> routePrices)
        {
            var mainPrice = routePrices[0];
            var bridgePrice = routePrices[1];
            var mainToBridgePrice = routePrices[2];

            var initBaseAmount = 100;
            //base => main => bridge => base

            var mainAmount = FloorAmount(100 / ParsePrice(mainPrice.AskPrice)); // buy main
            var bridgeAmount = FloorAmount(mainAmount * ParsePrice(mainToBridgePrice.BidPrice)); // sell main to get btc
            var outBaseAmount = bridgeAmount * ParsePrice(bridgePrice.BidPrice);
            var potentialProfit = FloorAmount(outBaseAmount - initBaseAmount);
            var message = $@"
            Profit: {potentialProfit}
            Route: {string.Join(">", route)}
            Price: {string.Join(">", new List<string> { mainPrice.AskPrice, mainToBridgePrice.BidPrice, bridgePrice.BidPrice })}
            Amount: {string.Join(">", new List<decimal> { mainAmount, bridgeAmount, outBaseAmount }.Select(x => x.ToString("G29")))}";
            _logTrace.LogInformation(message);
            if (potentialProfit > 1)
            {
                var res = await _telegramService.SendMessage(_telegramConfig.BotToken
                     , new TelegramMessage(_telegramConfig.ChatId, message));
            }

            bridgeAmount = FloorAmount(100 / ParsePrice(bridgePrice.AskPrice)); // buy btc
            mainAmount = FloorAmount(bridgeAmount / ParsePrice(mainToBridgePrice.AskPrice)); // buy main
            outBaseAmount = mainAmount * ParsePrice(mainPrice.BidPrice);
            potentialProfit = FloorAmount(outBaseAmount - initBaseAmount);
            message = $@"
            Profit: {potentialProfit}
            Route: {string.Join(">", new List<string> { route[0], route[2], route[1] })}
            Price: {string.Join(">", new List<string> { bridgePrice.AskPrice, mainToBridgePrice.AskPrice, mainPrice.BidPrice })}
            Amount: {string.Join(">", new List<decimal> { bridgeAmount, mainAmount, outBaseAmount }.Select(x => x.ToString("G29")))}";
            _logTrace.LogInformation(message);
            if (potentialProfit > 1)
            {
                var res = await _telegramService.SendMessage(_telegramConfig.BotToken
                     , new TelegramMessage(_telegramConfig.ChatId, message));
            }
        }

        static decimal FloorAmount(decimal num)
        {
            if (num > 1) return Math.Floor(num * POW4) / POW4;
            return Math.Floor(num * POW6) / POW6;
        }

        decimal ParsePrice(string price)
        {
            return decimal.Parse(price ?? "0");
        }
    }
}