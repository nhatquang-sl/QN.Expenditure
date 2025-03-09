using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Application.Grid.DTOs;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid
{
    public class UpdateSpotGridTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly ISender _sender;
        private readonly SpotGridDto _spotGridCreated;
        private readonly DateTime _startedAt = DateTime.UtcNow;

        public UpdateSpotGridTests()
        {
            Mock<IKuCoinService> kuCoinServiceMock = new();
            ServiceCollection.AddSingleton(kuCoinServiceMock.Object);

            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
            _currentUser = GetService<ICurrentUser>();

            _spotGridCreated = _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 5,
                SpotGridMode.ARITHMETIC, 100,
                110, 30)).Result;
        }

        [Fact]
        public async void Success()
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = 50,
                UpperPrice = 60,
                TriggerPrice = 40,
                NumberOfGrids = 9,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = 90
            });

            // Assert
            res.ShouldNotBeNull();
            res.Id.ShouldBeGreaterThan(0);
            res.UserId.ShouldBe(_currentUser.Id);
            res.Symbol.ShouldBe("BTCUSDT");
            res.CreatedAt.ShouldBeGreaterThan(_startedAt);
            res.CreatedAt.ShouldBeLessThan(DateTime.UtcNow);
            res.LowerPrice.ShouldBe(50);
            res.UpperPrice.ShouldBe(60);
            res.TriggerPrice.ShouldBe(40);
            res.NumberOfGrids.ShouldBe(9);
            res.GridMode.ShouldBe(SpotGridMode.GEOMETRIC);
            res.Investment.ShouldBe(90);
            res.TakeProfit.ShouldBeNull();
            res.StopLoss.ShouldBeNull();
            res.Status.ShouldBe(SpotGridStatus.NEW);

            var entity = _context.SpotGrids.FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();
            entity.DeletedAt.ShouldBeNull();
            entity.UpdatedAt.ShouldBeGreaterThan(_startedAt);
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
        [InlineData(110, 100, 0.2272)]
        [InlineData(120, 100, 0.2083)]
        // triggerQty = (0.25m*investment/triggerPrice)
        public async void Step_Initial_Success(decimal triggerPrice, decimal investment, decimal triggerQty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = 30,
                UpperPrice = 50,
                TriggerPrice = triggerPrice,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = investment
            });

            // Assert
            var steps = _context.SpotGridSteps.Where(s => s.SpotGridId == res.Id);
            var initStep = steps.FirstOrDefault(step => step.Type == SpotGridStepType.Initial);
            initStep.ShouldNotBeNull();
            initStep.BuyPrice.ShouldBe(triggerPrice);
            initStep.SellPrice.ShouldBe(triggerPrice);
            initStep.Qty.ShouldBe(triggerQty);
            initStep.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
        }

        [Theory]
        [InlineData(110)]
        [InlineData(120)]
        public async void Step_TakeProfit_Success(decimal takeProfit)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = 30,
                UpperPrice = 50,
                TriggerPrice = 40,
                TakeProfit = takeProfit,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = 100
            });

            // Assert
            var steps = _context.SpotGridSteps.Where(s => s.SpotGridId == res.Id).ToList();
            var takeProfitStep = steps.FirstOrDefault(step => step.Type == SpotGridStepType.TakeProfit);
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
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = 30,
                UpperPrice = 50,
                TriggerPrice = 40,
                StopLoss = stopLoss,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = 100
            });

            // Assert
            var steps = _context.SpotGridSteps.Where(s => s.SpotGridId == res.Id).ToList();
            var stopLossStep = steps.FirstOrDefault(step => step.Type == SpotGridStepType.StopLoss);
            stopLossStep.ShouldNotBeNull();
            stopLossStep.BuyPrice.ShouldBe(stopLoss);
            stopLossStep.SellPrice.ShouldBe(stopLoss);
            stopLossStep.Qty.ShouldBe(0);
            stopLossStep.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
        }
    }
}