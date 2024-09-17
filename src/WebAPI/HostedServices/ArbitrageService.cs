using Lib.ExternalServices.Bnd;
using Lib.ExternalServices.Telegram;
using Lib.ExternalServices.Telegram.Models;
using Microsoft.Extensions.Options;

namespace WebAPI.HostedServices
{
    public class ArbitrageService(IBndService bndService, ITelegramService telegramService, IOptions<TelegramServiceConfig> telegramConfig) : BackgroundService
    {
        private readonly IBndService _bndService = bndService;
        private readonly ITelegramService _telegramService = telegramService;
        private readonly TelegramServiceConfig _telegramConfig = telegramConfig.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var baseToken = "USDT";
            var bridgeToken = "BTC";
            var mainTokens = new List<string> { "FTM", "TIA", "DYM" };
            await Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var tasks = mainTokens.Select(async mainToken =>
                        {
                            var tokenPrices = await _bndService.GetTickerPrice($"[\"{mainToken}{baseToken}\",\"{mainToken}{bridgeToken}\",\"{bridgeToken}{baseToken}\"]");
                            var tokenPrice = decimal.Parse(tokenPrices.FirstOrDefault(x => x.Symbol == $"{mainToken}{baseToken}")?.Price ?? "0");
                            var bridgePrice = decimal.Parse(tokenPrices.FirstOrDefault(x => x.Symbol == $"{bridgeToken}{baseToken}")?.Price ?? "0");
                            var mainToBridgePrice = decimal.Parse(tokenPrices.FirstOrDefault(x => x.Symbol == $"{mainToken}{bridgeToken}")?.Price ?? "0");
                            var bridgeToMainPrice = 1 / mainToBridgePrice;

                            await Task.WhenAll(
                                CheckArbitrage([baseToken, mainToken, bridgeToken], [tokenPrice, mainToBridgePrice, bridgePrice])
                                , CheckArbitrage([baseToken, bridgeToken, mainToken], [bridgePrice, bridgeToMainPrice, tokenPrice]));
                        });

                        await Task.WhenAll(tasks);
                    }
                    catch (Exception ex)
                    {

                    }
                    await Task.Delay(60 * 1000);
                }
            }, stoppingToken);
        }

        async Task CheckArbitrage(List<string> route, List<decimal> routePrices)
        {
            var mainPrice = routePrices[0];
            var mainToBridgePrice = routePrices[1];
            var bridgePrice = routePrices[2];

            var initBaseAmount = 100;
            var mainAmount = initBaseAmount / mainPrice;
            var bridgeAmount = mainAmount * mainToBridgePrice;
            var potentialProfit = bridgeAmount * bridgePrice - initBaseAmount;

            Console.WriteLine($"Profit: {potentialProfit}\nRoute: {string.Join(">", route)}\nPrice: {string.Join(">", routePrices)}");
            if (potentialProfit > 1)
            {
                await _telegramService.SendMessage(_telegramConfig.BotToken
                    , new TelegramMessage(_telegramConfig.ChatId, $"Profit: {potentialProfit}\nRoute: {string.Join(">", route)}\nPrice: {string.Join(">", routePrices)}"));
            }
        }
    }
}
