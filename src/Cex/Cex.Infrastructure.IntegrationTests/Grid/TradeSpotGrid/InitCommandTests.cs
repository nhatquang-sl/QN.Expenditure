using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.TradeSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using Lib.ExternalServices.KuCoin.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid.TradeSpotGrid
{
    public class InitCommandTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly Mock<IKuCoinService> _kuCoinServiceMock = new();
        private readonly ISender _sender;

        public InitCommandTests()
        {
            _kuCoinServiceMock.Setup(x => x.PlaceOrder(It.IsAny<PlaceOrderRequest>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_order_id");

            _kuCoinServiceMock.Setup(x =>
                    x.GetOrderDetails(It.Is<string>(o => o == "completed_order_id"), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "completed_order_id",
                    Type = "limit",
                    Side = "buy",
                    Price = "50.0",
                    IsActive = false,
                    Size = "0.5",
                    DealSize = "0.5",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });

            _kuCoinServiceMock.Setup(x =>
                    x.GetOrderDetails(It.Is<string>(o => o == "in_completed_order_id"), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails { IsActive = true });

            ServiceCollection.AddSingleton(_kuCoinServiceMock.Object);

            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
        }

        /// <summary>
        ///     Initial Step is AwaitingBuy and LowestPrice below Trigger * 1.1
        ///     Expect:
        ///     - Grid status unchanged
        ///     - Grid quote balance decreased by BuyPrice * Qty
        ///     - Initial step order id is not null
        ///     - Initial step status changed to BuyOrderPlaced
        ///     - KuCoinService.PlaceOrder called once
        /// </summary>
        [Theory]
        [InlineData(55)]
        [InlineData(54)]
        public async Task HandleAwaitingBuy_Should_PlaceOrder_When_LowestPriceBelowThreshold(decimal lowestPrice)
        {
            // Arrange
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var kline = new Kline { LowestPrice = lowestPrice };

            // Act
            await _sender.Send(new InitCommand(originalGrid, kline));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var initialStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);

            grid.Status.ShouldBe(SpotGridStatus.NEW);
            grid.QuoteBalance.ShouldBe(75);

            initialStep.OrderId.ShouldNotBeNullOrEmpty();
            initialStep.OrderId.ShouldBe("fake_order_id");
            initialStep.Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.Is<PlaceOrderRequest>(req =>
                req.Symbol == originalGrid.Symbol &&
                req.Side == "buy" &&
                req.Type == "limit" &&
                req.Price == initialStep.BuyPrice.ToString(CultureInfo.InvariantCulture) &&
                req.Size == initialStep.Qty.ToString(CultureInfo.InvariantCulture)
            ), It.IsAny<KuCoinConfig>()), Times.Once);
        }

        /// <summary>
        ///     Initial Step is AwaitingBuy but LowestPrice > Trigger * 1.1
        ///     Expect:
        ///     - Grid status unchanged
        ///     - Initial step status unchanged
        ///     - Initial step order id is null
        /// </summary>
        [Theory]
        [InlineData(56)]
        [InlineData(57)]
        public async void HandleAwaitingBuy_Should_UnChange_When_LowestPriceAboveThreshold(decimal lowestPrice)
        {
            // Arrange
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var kline = new Kline { LowestPrice = lowestPrice };

            // Act
            await _sender.Send(new InitCommand(originalGrid, kline));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var initialStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);
            grid.Status.ShouldBe(SpotGridStatus.NEW);
            initialStep.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            initialStep.OrderId.ShouldBeNull();
        }

        /// <summary>
        ///     Initial Step is BuyOrderPlaced but OrderId is null by a mistake
        ///     Expect:
        ///     - Grid status unchanged
        ///     - Initial step status unchanged
        /// </summary>
        [Fact]
        public async Task HandleBuyOrderPlaced_Should_UnChange_When_OrderId_NullOrWhitespace()
        {
            // Arrange
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var originalInitialStep = originalGrid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);
            originalInitialStep.OrderId = null;
            originalInitialStep.Status = SpotGridStepStatus.BuyOrderPlaced;
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new InitCommand(originalGrid, new Kline()));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var initialStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);
            grid.Status.ShouldBe(SpotGridStatus.NEW);
            initialStep.Status.ShouldBe(originalInitialStep.Status);
        }

        /// <summary>
        ///     Initial Step is BuyOrderPlaced and Order in-completed
        ///     Expect:
        ///     Grid should be un-change
        ///     - Status should be NEW
        ///     - Base balance should be 0
        ///     Initial step should be un-change
        ///     - OrderId should be in_completed_order_id
        ///     - Status should be BuyOrderPlaced
        ///     KuCoinService.GetOrderDetails called once
        /// </summary>
        [Fact]
        public async Task HandleBuyOrderPlaced_Should_UnChange_When_OrderInCompleted()
        {
            // Arrange
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var originalInitialStep = originalGrid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);
            originalInitialStep.OrderId = "in_completed_order_id";
            originalInitialStep.Status = SpotGridStepStatus.BuyOrderPlaced;
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new InitCommand(originalGrid, new Kline()));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var initialStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);

            grid.Status.ShouldBe(SpotGridStatus.NEW);
            grid.BaseBalance.ShouldBe(0);

            initialStep.OrderId.ShouldBe("in_completed_order_id");
            initialStep.Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            _kuCoinServiceMock.Verify(
                s => s.GetOrderDetails(
                    It.Is<string>(orderId => orderId == "in_completed_order_id"),
                    It.IsAny<KuCoinConfig>()), Times.Once);
        }

        /// <summary>
        ///     Initial Step is BuyOrderPlaced and Order is completed
        ///     Expect:
        ///     - Grid status should be RUNNING
        ///     - Grid base balance should be 0.5
        ///     - Initial step order id should be null
        ///     - Initial step status changed to AwaitingSell
        ///     - KuCoinService.GetOrderDetails called once
        /// </summary>
        [Fact]
        public async Task HandleBuyOrderPlaced_Should_Update_When_OrderCompleted()
        {
            // Arrange
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var originalInitialStep = originalGrid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);
            originalInitialStep.OrderId = "completed_order_id";
            originalInitialStep.Status = SpotGridStepStatus.BuyOrderPlaced;
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new InitCommand(originalGrid, new Kline()));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var initialStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.Initial);

            grid.Status.ShouldBe(SpotGridStatus.RUNNING);
            grid.BaseBalance.ShouldBe(0.5m);

            initialStep.OrderId.ShouldBeNull();
            initialStep.Status.ShouldBe(SpotGridStepStatus.AwaitingSell);

            _kuCoinServiceMock.Verify(
                s => s.GetOrderDetails(
                    It.Is<string>(orderId => orderId == "completed_order_id"),
                    It.IsAny<KuCoinConfig>()), Times.Once);
        }
    }
}