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

namespace Cex.Infrastructure.IntegrationTests.Grid.UpdateSpotGrid
{
    // Initial Step Placed
    // Initial Step Matched
    public class GridNewStateTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;

        private readonly CreateSpotGridCommand _createCommand = new("BTCUSDT", 60, 70
            , 50, 5, SpotGridMode.ARITHMETIC, 100, 110, 30);

        private readonly ICurrentUser _currentUser;
        private readonly Mock<IKuCoinService> _kuCoinServiceMock = new();
        private readonly ISender _sender;
        private readonly SpotGridDto _spotGridCreated;
        private readonly DateTime _startedAt = DateTime.UtcNow;

        public GridNewStateTests()
        {
            ServiceCollection.AddSingleton(_kuCoinServiceMock.Object);

            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
            _currentUser = GetService<ICurrentUser>();

            _spotGridCreated = _sender.Send(_createCommand).Result;
        }

        [Theory]
        [InlineData(110, 100, 0.2272)]
        [InlineData(120, 100, 0.2083)]
        public async void UpdateTriggerPrice_UnChangeQuoteBalance(decimal triggerPrice,
            decimal investment, decimal triggerQty)
        {
        }

        // public async void UpdateTriggerPrice_ChangeInitialStep()
        // {
        // }
        //
        // public async void UpdateTriggerPrice_ChangeNormalSteps()
        // {
        // }
        //
        // public async void UpdateInvestment_ChangeQuoteBalance()
        // {
        // }
        //
        // public async void UpdateInvestment_ChangeInitialStep()
        // {
        // }
        //
        // public async void UpdateInvestment_ChangeNormalSteps()
        // {
        // }

        [Theory]
        [InlineData(110, 100, 0.2272)]
        [InlineData(120, 100, 0.2083)]
        public async void UpdateInitialStep_AwaitingBuy_WithoutCancelOrder(decimal triggerPrice,
            decimal investment, decimal triggerQty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = _createCommand.LowerPrice,
                UpperPrice = _createCommand.UpperPrice,
                TriggerPrice = triggerPrice,
                NumberOfGrids = _createCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = investment
            });

            // Assert
            var initStepUpdated = _context.SpotGridSteps.First(x => x.SpotGridId == res.Id &&
                                                                    x.Type == SpotGridStepType.Initial);
            initStepUpdated.ShouldNotBeNull();
            initStepUpdated.BuyPrice.ShouldBe(triggerPrice);
            initStepUpdated.SellPrice.ShouldBe(triggerPrice);
            initStepUpdated.Qty.ShouldBe(triggerQty);
            initStepUpdated.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        [Theory]
        [InlineData(110, 100, 0.2272)]
        [InlineData(120, 200, 0.4166)]
        [InlineData(100, 150, 0.375)]
        public async void UpdateInitialStep_BuyOrderPlaced_CancelOrder(decimal triggerPrice,
            decimal investment, decimal triggerQty)
        {
            // Arrange
            var initialStep = _context.SpotGridSteps.First(x => x.SpotGridId == _spotGridCreated.Id &&
                                                                x.Type == SpotGridStepType.Initial);
            // Initial Step has placed success
            initialStep.OrderId = "fakeOrderId";
            initialStep.Status = SpotGridStepStatus.BuyOrderPlaced;

            _context.SpotGridSteps.Update(initialStep);
            await _context.SaveChangesAsync(default);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = _createCommand.LowerPrice,
                UpperPrice = _createCommand.UpperPrice,
                TriggerPrice = triggerPrice,
                NumberOfGrids = _createCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = investment
            });

            // Assert
            var initStepUpdated = _context.SpotGridSteps.First(x => x.SpotGridId == res.Id &&
                                                                    x.Type == SpotGridStepType.Initial);
            initStepUpdated.ShouldNotBeNull();
            initStepUpdated.BuyPrice.ShouldBe(triggerPrice);
            initStepUpdated.SellPrice.ShouldBe(triggerPrice);
            initStepUpdated.Qty.ShouldBe(triggerQty);
            initStepUpdated.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.Is<string>(x => x == "fakeOrderId"),
                    It.IsAny<KuCoinConfig>()),
                Times.Once);
        }
    }
}