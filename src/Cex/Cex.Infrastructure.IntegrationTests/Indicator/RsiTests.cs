using Cex.Application.Indicator.Commands.Rsi;
using MediatR;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Indicator
{
    public class RsiTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;

        public RsiTests()
        {
            _sender = GetService<ISender>();
        }

        [Fact]
        public async Task Success()
        {
            // Arrange
            var command = new RsiCommand(Data.Candles);
            var lastCandle = Data.Candles[^2];

            // Act
            var res = await _sender.Send(command);

            // Assert
            Assert.NotNull(res);
            Assert.NotEmpty(res);
            lastCandle.OpenTime.ShouldBe(DateTime.Parse("4/10/2025 05:05:00 AM"));
            res[Data.Candles[^2].OpenTime].ShouldBe(58.09m);
            res[Data.Candles[^3].OpenTime].ShouldBe(52.06m);
            res[Data.Candles[^4].OpenTime].ShouldBe(55.39m);
            res[Data.Candles[^5].OpenTime].ShouldBe(53.72m);
            res[Data.Candles[^6].OpenTime].ShouldBe(53.44m);
        }
    }
}