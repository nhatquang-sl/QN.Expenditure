using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid
{
    public class CreateSpotGridTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly ISender _sender;

        public CreateSpotGridTests()
        {
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async void Success()
        {
            // Arrange
            var startedAt = DateTime.UtcNow;
            var command = new CreateSpotGridCommand("BTCUSDT", 60, 70,
                50, 10, SpotGridMode.ARITHMETIC,
                100, 110, 30);

            // Act
            var res = await _sender.Send(command);

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

            var entity = _context.SpotGrids
                .Include(x => x.GridSteps)
                .FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();
            entity.DeletedAt.ShouldBeNull();
            entity.Id.ShouldBe(res.Id);
            entity.UserId.ShouldBe(res.UserId);
            entity.Symbol.ShouldBe(res.Symbol);
            entity.CreatedAt.ShouldBe(res.CreatedAt);
            entity.UpdatedAt.ShouldBe(res.UpdatedAt);
            entity.LowerPrice.ShouldBe(res.LowerPrice);
            entity.UpperPrice.ShouldBe(res.UpperPrice);
            entity.TriggerPrice.ShouldBe(res.TriggerPrice);
            entity.NumberOfGrids.ShouldBe(res.NumberOfGrids);
            entity.GridMode.ShouldBe(res.GridMode);
            entity.Investment.ShouldBe(res.Investment);
            entity.TakeProfit.ShouldBe(res.TakeProfit);
            entity.StopLoss.ShouldBe(res.StopLoss);
            entity.Status.ShouldBe(SpotGridStatus.NEW);
            entity.BaseBalance.ShouldBe(0);
            entity.QuoteBalance.ShouldBe(res.Investment);
            entity.Profit.ShouldBe(0);
        }

        [Theory]
        [InlineData(110, 100, 0.2272)]
        [InlineData(120, 100, 0.2083)]
        // triggerQty = (0.25m*investment/triggerPrice)
        public async void Step_Initial_Success(decimal triggerPrice, decimal investment, decimal triggerQty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70,
                triggerPrice, 10, SpotGridMode.ARITHMETIC,
                investment, 110, 30));

            // Assert
            var entity = _context.SpotGrids
                .Include(x => x.GridSteps)
                .FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();

            var initStep = entity.GridSteps.FirstOrDefault(step => step.Type == SpotGridStepType.Initial);
            initStep.ShouldNotBeNull();
            initStep.BuyPrice.ShouldBe(triggerPrice);
            initStep.SellPrice.ShouldBe(triggerPrice);
            initStep.Qty.ShouldBe(triggerQty);
            initStep.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
        }

        public static IEnumerable<object[]> GetGridStepTestData()
        {
            yield return new object[]
            {
                60m, 70m, new decimal[] { 60, 62, 64, 66, 68 },
                new decimal[] { 62, 64, 66, 68, 70 },
                new[] { 0.25m, 0.2419m, 0.2343m, 0.2272m, 0.2205m }
            };
            yield return new object[]
            {
                55m, 70m, new decimal[] { 55, 58, 61, 64, 67 },
                new decimal[] { 58, 61, 64, 67, 70 },
                new[] { 0.2727m, 0.2586m, 0.2459m, 0.2343m, 0.2238m }
            };
        }

        [Theory]
        [MemberData(nameof(GetGridStepTestData))]
        public async void Step_Normal_Success(decimal lowerPrice, decimal upperPrice,
            decimal[] buyPrices, decimal[] sellPrices, decimal[] qties)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new CreateSpotGridCommand("BTCUSDT", lowerPrice, upperPrice,
                100, 5, SpotGridMode.ARITHMETIC,
                100, 110, 30));

            // Assert
            var entity = _context.SpotGrids
                .Include(x => x.GridSteps)
                .FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();

            var steps = entity.GridSteps.Where(step => step.Type == SpotGridStepType.Normal)
                .OrderBy(step => step.BuyPrice)
                .ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(buyPrices[0]);
            steps[0].SellPrice.ShouldBe(sellPrices[0]);
            steps[0].Qty.ShouldBe(qties[0]);
            steps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[1].BuyPrice.ShouldBe(buyPrices[1]);
            steps[1].SellPrice.ShouldBe(sellPrices[1]);
            steps[1].Qty.ShouldBe(qties[1]);
            steps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[2].BuyPrice.ShouldBe(buyPrices[2]);
            steps[2].SellPrice.ShouldBe(sellPrices[2]);
            steps[2].Qty.ShouldBe(qties[2]);
            steps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[3].BuyPrice.ShouldBe(buyPrices[3]);
            steps[3].SellPrice.ShouldBe(sellPrices[3]);
            steps[3].Qty.ShouldBe(qties[3]);
            steps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[4].BuyPrice.ShouldBe(buyPrices[4]);
            steps[4].SellPrice.ShouldBe(sellPrices[4]);
            steps[4].Qty.ShouldBe(qties[4]);
            steps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
        }

        [Theory]
        [InlineData(110)]
        [InlineData(120)]
        public async void Step_TakeProfit_Success(decimal takeProfit)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70,
                50, 10, SpotGridMode.ARITHMETIC,
                100, takeProfit, 30));

            // Assert
            var entity = _context.SpotGrids
                .Include(x => x.GridSteps)
                .FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();

            var takeProfitStep = entity.GridSteps.FirstOrDefault(step => step.Type == SpotGridStepType.TakeProfit);
            takeProfitStep.ShouldNotBeNull();
            takeProfitStep.BuyPrice.ShouldBe(takeProfit);
            takeProfitStep.SellPrice.ShouldBe(takeProfit);
            takeProfitStep.Qty.ShouldBe(0);
            takeProfitStep.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
        }

        [Theory]
        [InlineData(50)]
        [InlineData(60)]
        public async void Step_StopLoss_Success(decimal stopLoss)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70,
                50, 10, SpotGridMode.ARITHMETIC,
                100, 110, stopLoss));

            // Assert
            var entity = _context.SpotGrids
                .Include(x => x.GridSteps)
                .FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();

            var stopLossStep = entity.GridSteps.FirstOrDefault(step => step.Type == SpotGridStepType.StopLoss);
            stopLossStep.ShouldNotBeNull();
            stopLossStep.BuyPrice.ShouldBe(stopLoss);
            stopLossStep.SellPrice.ShouldBe(stopLoss);
            stopLossStep.Qty.ShouldBe(0);
            stopLossStep.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
        }

        [Theory]
        [InlineData("")]
        [InlineData("US")]
        [InlineData("USDT")]
        public async void FailsSymbolTooShort(string symbol)
        {
            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(()
                => _sender.Send(
                    new CreateSpotGridCommand(symbol, 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30)));

            // Assert
            exception.Message.ShouldBe(
                """[{"name":"symbol","errors":["Symbol must be at least 5 characters."]}]""");
        }

        [Theory]
        [InlineData("1234567USDT")]
        [InlineData("12345678USDT")]
        public async void FailsSymbolTooLong(string symbol)
        {
            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(()
                => _sender.Send(
                    new CreateSpotGridCommand(symbol, 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30)));

            // Assert
            exception.Message.ShouldBe(
                """[{"name":"symbol","errors":["Symbol has reached a maximum of 10 characters."]}]""");
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(60, 50)]
        public async void FailsUpperPriceLessOrEqualLowerPrice(decimal lowerPrice, decimal upperPrice)
        {
            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(()
                => _sender.Send(new CreateSpotGridCommand("BTCUSDT", lowerPrice, upperPrice, 50, 10,
                    SpotGridMode.ARITHMETIC, 100, 110, 30)));

            // Assert
            exception.Message.ShouldBe(
                """[{"name":"upperPrice","errors":["Upper Price must be greater than Lower Price."]}]""");
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(40, 50)]
        public async void FailsTakeProfitLessOrEqualStopLoss(decimal takeProfit, decimal stopLoss)
        {
            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(()
                => _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100,
                    takeProfit, stopLoss)));

            // Assert
            exception.Message.ShouldBe(
                """[{"name":"takeProfit","errors":["Take Profit must be greater than Stop Loss."]}]""");
        }

        [Theory]
        [InlineData(50, 50)]
        [InlineData(40, 50)]
        public async void FailsSymbolMissingTakeProfitLessOrEqualStopLoss(decimal takeProfit, decimal stopLoss)
        {
            // Act
            var exception = await Should.ThrowAsync<UnprocessableEntityException>(()
                => _sender.Send(new CreateSpotGridCommand("", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, takeProfit,
                    stopLoss)));

            // Assert
            exception.Message.ShouldBe(
                """[{"name":"symbol","errors":["Symbol must be at least 5 characters."]},{"name":"takeProfit","errors":["Take Profit must be greater than Stop Loss."]}]""");
        }
    }
}