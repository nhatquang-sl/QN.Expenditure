using Application.BnbSpotGrid.Commands.CreateSpotGrid;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Domain.Entities;
using MediatR;
using Shouldly;

namespace Infrastructure.IntegrationTests.BnbSpotGrid
{
    public class CreateSpotGridTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IApplicationDbContext _context;

        public CreateSpotGridTests() : base()
        {
            _sender = GetService<ISender>();
            _context = GetService<IApplicationDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async void Success()
        {
            // Arrange
            var startedAt = DateTime.UtcNow;

            // Act
            var res = await _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30));

            // Assert
            res.ShouldNotBeNull();
            res.Id.ShouldBeGreaterThan(0);
            res.UserId.ShouldBe(_currentUser.Id);
            res.Symbol.ShouldBe("BTCUSDT");
            res.CreatedAt.ShouldBeGreaterThan(startedAt);
            res.CreatedAt.ShouldBeLessThan(DateTime.UtcNow);
            res.LowerPrice.ShouldBe(60);
            res.UpperPrice.ShouldBe(70);
            res.TriggerPrice.ShouldBe(50);
            res.NumberOfGrids.ShouldBe(10);
            res.GridMode.ShouldBe(SpotGridMode.ARITHMETIC);
            res.Investment.ShouldBe(100);
            res.TakeProfit.ShouldBe(110);
            res.StopLoss.ShouldBe(30);
            res.Status.ShouldBe(SpotGridStatus.NEW);

            var entity = _context.SpotGrids.Where(x => x.UserId == _currentUser.Id && x.Id == res.Id).FirstOrDefault();
            entity.ShouldNotBeNull();
            entity.DeletedAt.ShouldBeNull();
            res.Id.ShouldBe(entity.Id);
            res.UserId.ShouldBe(entity.UserId);
            res.Symbol.ShouldBe(entity.Symbol);
            res.CreatedAt.ShouldBe(entity.CreatedAt);
            res.UpdatedAt.ShouldBe(entity.UpdatedAt);
            res.LowerPrice.ShouldBe(entity.LowerPrice);
            res.UpperPrice.ShouldBe(entity.UpperPrice);
            res.TriggerPrice.ShouldBe(entity.TriggerPrice);
            res.NumberOfGrids.ShouldBe(entity.NumberOfGrids);
            res.GridMode.ShouldBe(entity.GridMode);
            res.Investment.ShouldBe(entity.Investment);
            res.TakeProfit.ShouldBe(entity.TakeProfit);
            res.StopLoss.ShouldBe(entity.StopLoss);
            res.Status.ShouldBe(entity.Status);
        }

        [Theory]
        [InlineData("")]
        [InlineData("US")]
        [InlineData("USDT")]
        public async void Fails_Symbol_Too_Short(string symbol)
        {
            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(()
                => _sender.Send(new CreateSpotGridCommand(symbol, 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30)));

            // Assert
            exception.Message.ShouldBe(@"{""symbol"":""Symbol must be at least 5 characters.""}");
        }

        [Theory]
        [InlineData("1234567USDT")]
        [InlineData("12345678USDT")]
        public async void Fails_Symbol_Too_Long(string symbol)
        {
            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(()
                => _sender.Send(new CreateSpotGridCommand(symbol, 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30)));

            // Assert
            exception.Message.ShouldBe(@"{""symbol"":""Symbol has reached a maximum of 10 characters.""}");
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(60, 50)]
        public async void Fails_UpperPrice_LessOrEqual_LowerPrice(decimal lowerPrice, decimal upperPrice)
        {
            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(()
                => _sender.Send(new CreateSpotGridCommand("BTCUSDT", lowerPrice, upperPrice, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30)));

            // Assert
            exception.Message.ShouldBe(@"{""upperPrice"":""Upper Price must be greater than Lower Price.""}");
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(40, 50)]
        public async void Fails_TakeProfit_LessOrEqual_StopLoss(decimal takeProfit, decimal stopLoss)
        {
            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(()
                => _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, takeProfit, stopLoss)));

            // Assert
            exception.Message.ShouldBe(@"{""takeProfit"":""Take Profit must be greater than Stop Loss.""}");
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(40, 50)]
        public async void Fails_Symbol_Missing_TakeProfit_LessOrEqual_StopLoss(decimal takeProfit, decimal stopLoss)
        {
            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(()
                => _sender.Send(new CreateSpotGridCommand("", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, takeProfit, stopLoss)));

            // Assert
            exception.Message.ShouldBe(@"{""symbol"":""Symbol must be at least 5 characters."",""takeProfit"":""Take Profit must be greater than Stop Loss.""}");
        }
    }
}
