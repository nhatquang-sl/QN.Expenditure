using Cex.Application.Indicator.Commands;
using MediatR;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Indicator
{
    public class BollingerBandsTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;

        public BollingerBandsTests()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async Task Success()
        {
            // Arrange
            var command = new BollingerBandsCommand(Data.Candles);
            var lastCandle = Data.Candles[^2];

            // Act
            var res = await _sender.Send(command);

            // Assert
            Assert.NotNull(res);
            Assert.NotEmpty(res);
            lastCandle.OpenTime.ShouldBe(DateTime.Parse("4/10/2025 05:05:00 AM"));
            res[Data.Candles[^2].OpenTime].ShouldBe(new BollingerBands(81798.4m, 82061.1m, 81535.6m));
            res[Data.Candles[^3].OpenTime].ShouldBe(new BollingerBands(81776.6m, 82011.9m, 81541.2m));
            res[Data.Candles[^4].OpenTime].ShouldBe(new BollingerBands(81761.1m, 81991m, 81531.1m));
            res[Data.Candles[^5].OpenTime].ShouldBe(new BollingerBands(81738.8m, 81955.3m, 81522.2m));
            res[Data.Candles[^6].OpenTime].ShouldBe(new BollingerBands(81727.4m, 81916.5m, 81538.2m));
            res[Data.Candles[^7].OpenTime].ShouldBe(new BollingerBands(81728.2m, 81921.2m, 81535.1m));
            res[Data.Candles[^8].OpenTime].ShouldBe(new BollingerBands(81740.6m, 81986.5m, 81494.6m));
        }
    }
}