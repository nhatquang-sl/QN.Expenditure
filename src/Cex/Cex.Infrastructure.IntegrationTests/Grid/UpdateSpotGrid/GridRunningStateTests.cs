using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Application.Grid.DTOs;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid.UpdateSpotGrid
{
    // Init Step is AwaitingSell => Can't update
    // Update normal steps 
    //  - 
    public class GridRunningStateTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;

        private readonly CreateSpotGridCommand _createCommand = new("BTCUSDT", 40, 70
            , 50, 5, SpotGridMode.ARITHMETIC, 100, 110, 30);

        private readonly Mock<IKuCoinService> _kuCoinServiceMock = new();
        private readonly ISender _sender;
        private readonly SpotGridDto _spotGridCreated;

        public GridRunningStateTests()
        {
            ServiceCollection.AddSingleton(_kuCoinServiceMock.Object);

            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();

            _spotGridCreated = _sender.Send(_createCommand).Result;

            _context.SpotGrids
                .Where(x => x.Id == _spotGridCreated.Id)
                .ExecuteUpdate(setters => setters
                        .SetProperty(x => x.Status, SpotGridStatus.RUNNING) // Update only the Status property
                );

            // Initial Step has placed success
            _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Initial)
                .ExecuteUpdate(setters => setters
                    .SetProperty(s => s.Status, SpotGridStepStatus.AwaitingSell)
                );

            var grid = _context.SpotGrids.Local
                .First(s => s.Id == _spotGridCreated.Id);
            _context.SpotGrids.Entry(grid).State = EntityState.Detached;
        }

        [Fact]
        public async void UpdateInitialStep_AwaitingSell_ToDca()
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = _createCommand.LowerPrice,
                UpperPrice = _createCommand.UpperPrice,
                TriggerPrice = 55,
                NumberOfGrids = _createCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = 110
            });

            // Assert
            var initStepUpdated = _context.SpotGridSteps
                .First(x => x.SpotGridId == res.Id && x.Type == SpotGridStepType.Initial);
            initStepUpdated.ShouldNotBeNull();
            initStepUpdated.BuyPrice.ShouldBe(55);
            initStepUpdated.SellPrice.ShouldBe(55);
            initStepUpdated.Qty.ShouldBe(0.5m);
            initStepUpdated.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        [Theory]
        [MemberData(nameof(GetGridStepTestData))]
        public async void UpdateNormalSteps_AwaitingBuy_Change(decimal lowerPrice, decimal upperPrice,
            decimal[] buyPrices, decimal[] sellPrices, decimal[] qty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = 40,
                NumberOfGrids = _spotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = _spotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(buyPrices[0]);
            steps[0].SellPrice.ShouldBe(sellPrices[0]);
            steps[0].Qty.ShouldBe(qty[0]);
            steps[0].OrderId.ShouldBeNull();
            steps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[1].BuyPrice.ShouldBe(buyPrices[1]);
            steps[1].SellPrice.ShouldBe(sellPrices[1]);
            steps[1].Qty.ShouldBe(qty[1]);
            steps[1].OrderId.ShouldBeNull();
            steps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[2].BuyPrice.ShouldBe(buyPrices[2]);
            steps[2].SellPrice.ShouldBe(sellPrices[2]);
            steps[2].Qty.ShouldBe(qty[2]);
            steps[2].OrderId.ShouldBeNull();
            steps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[3].BuyPrice.ShouldBe(buyPrices[3]);
            steps[3].SellPrice.ShouldBe(sellPrices[3]);
            steps[3].Qty.ShouldBe(qty[3]);
            steps[3].OrderId.ShouldBeNull();
            steps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[4].BuyPrice.ShouldBe(buyPrices[4]);
            steps[4].SellPrice.ShouldBe(sellPrices[4]);
            steps[4].Qty.ShouldBe(qty[4]);
            steps[4].OrderId.ShouldBeNull();
            steps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        [Fact]
        public async void UpdateNormalSteps_AwaitingBuy_NotChange()
        {
            // Arrange
            var originalSteps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = _spotGridCreated.LowerPrice,
                UpperPrice = _spotGridCreated.UpperPrice,
                TriggerPrice = 40,
                NumberOfGrids = _spotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = _spotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(originalSteps[0].BuyPrice);
            steps[0].SellPrice.ShouldBe(originalSteps[0].SellPrice);
            steps[0].Qty.ShouldBe(originalSteps[0].Qty);
            steps[0].OrderId.ShouldBe(originalSteps[0].OrderId);
            steps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[1].BuyPrice.ShouldBe(originalSteps[1].BuyPrice);
            steps[1].SellPrice.ShouldBe(originalSteps[1].SellPrice);
            steps[1].Qty.ShouldBe(originalSteps[1].Qty);
            steps[1].OrderId.ShouldBe(originalSteps[1].OrderId);
            steps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[2].BuyPrice.ShouldBe(originalSteps[2].BuyPrice);
            steps[2].SellPrice.ShouldBe(originalSteps[2].SellPrice);
            steps[2].Qty.ShouldBe(originalSteps[2].Qty);
            steps[2].OrderId.ShouldBe(originalSteps[2].OrderId);
            steps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[3].BuyPrice.ShouldBe(originalSteps[3].BuyPrice);
            steps[3].SellPrice.ShouldBe(originalSteps[3].SellPrice);
            steps[3].Qty.ShouldBe(originalSteps[3].Qty);
            steps[3].OrderId.ShouldBe(originalSteps[3].OrderId);
            steps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[4].BuyPrice.ShouldBe(originalSteps[4].BuyPrice);
            steps[4].SellPrice.ShouldBe(originalSteps[4].SellPrice);
            steps[4].Qty.ShouldBe(originalSteps[4].Qty);
            steps[4].OrderId.ShouldBe(originalSteps[4].OrderId);
            steps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        [Theory]
        [MemberData(nameof(GetGridStepTestData))]
        public async void UpdateNormalSteps_BuyOrderPlaced_Change(decimal lowerPrice, decimal upperPrice,
            decimal[] buyPrices, decimal[] sellPrices, decimal[] qty)
        {
            // Arrange
            var normalSteps = await _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToListAsync();
            for (var i = 0; i < normalSteps.Count; i++)
            {
                normalSteps[i].OrderId = $"fake_order_id_{i + 1}";
                normalSteps[i].Status = SpotGridStepStatus.BuyOrderPlaced;
            }

            await _context.SaveChangesAsync(default);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = 40,
                NumberOfGrids = _spotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = _spotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(buyPrices[0]);
            steps[0].SellPrice.ShouldBe(sellPrices[0]);
            steps[0].Qty.ShouldBe(qty[0]);
            steps[0].OrderId.ShouldBeNull();
            steps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[1].BuyPrice.ShouldBe(buyPrices[1]);
            steps[1].SellPrice.ShouldBe(sellPrices[1]);
            steps[1].Qty.ShouldBe(qty[1]);
            steps[1].OrderId.ShouldBeNull();
            steps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[2].BuyPrice.ShouldBe(buyPrices[2]);
            steps[2].SellPrice.ShouldBe(sellPrices[2]);
            steps[2].Qty.ShouldBe(qty[2]);
            steps[2].OrderId.ShouldBeNull();
            steps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[3].BuyPrice.ShouldBe(buyPrices[3]);
            steps[3].SellPrice.ShouldBe(sellPrices[3]);
            steps[3].Qty.ShouldBe(qty[3]);
            steps[3].OrderId.ShouldBeNull();
            steps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[4].BuyPrice.ShouldBe(buyPrices[4]);
            steps[4].SellPrice.ShouldBe(sellPrices[4]);
            steps[4].Qty.ShouldBe(qty[4]);
            steps[4].OrderId.ShouldBeNull();
            steps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.Is<string>(x => x == "fake_order_id_1"),
                    It.IsAny<KuCoinConfig>()),
                Times.Once);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.Is<string>(x => x == "fake_order_id_2"),
                    It.IsAny<KuCoinConfig>()),
                Times.Once);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.Is<string>(x => x == "fake_order_id_3"),
                    It.IsAny<KuCoinConfig>()),
                Times.Once);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.Is<string>(x => x == "fake_order_id_4"),
                    It.IsAny<KuCoinConfig>()),
                Times.Once);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.Is<string>(x => x == "fake_order_id_5"),
                    It.IsAny<KuCoinConfig>()),
                Times.Once);
        }

        [Fact]
        public async void UpdateNormalSteps_BuyOrderPlaced_NotChange()
        {
            // Arrange
            var normalSteps = await _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToListAsync();
            for (var i = 0; i < normalSteps.Count; i++)
            {
                normalSteps[i].OrderId = $"fake_order_id_{i + 1}";
                normalSteps[i].Status = SpotGridStepStatus.BuyOrderPlaced;
            }

            await _context.SaveChangesAsync(default);
            var originalSteps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = _spotGridCreated.LowerPrice,
                UpperPrice = _spotGridCreated.UpperPrice,
                TriggerPrice = 40,
                NumberOfGrids = _spotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = _spotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(originalSteps[0].BuyPrice);
            steps[0].SellPrice.ShouldBe(originalSteps[0].SellPrice);
            steps[0].Qty.ShouldBe(originalSteps[0].Qty);
            steps[0].OrderId.ShouldBe(originalSteps[0].OrderId);
            steps[0].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            steps[1].BuyPrice.ShouldBe(originalSteps[1].BuyPrice);
            steps[1].SellPrice.ShouldBe(originalSteps[1].SellPrice);
            steps[1].Qty.ShouldBe(originalSteps[1].Qty);
            steps[1].OrderId.ShouldBe(originalSteps[1].OrderId);
            steps[1].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            steps[2].BuyPrice.ShouldBe(originalSteps[2].BuyPrice);
            steps[2].SellPrice.ShouldBe(originalSteps[2].SellPrice);
            steps[2].Qty.ShouldBe(originalSteps[2].Qty);
            steps[2].OrderId.ShouldBe(originalSteps[2].OrderId);
            steps[2].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            steps[3].BuyPrice.ShouldBe(originalSteps[3].BuyPrice);
            steps[3].SellPrice.ShouldBe(originalSteps[3].SellPrice);
            steps[3].Qty.ShouldBe(originalSteps[3].Qty);
            steps[3].OrderId.ShouldBe(originalSteps[3].OrderId);
            steps[3].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            steps[4].BuyPrice.ShouldBe(originalSteps[4].BuyPrice);
            steps[4].SellPrice.ShouldBe(originalSteps[4].SellPrice);
            steps[4].Qty.ShouldBe(originalSteps[4].Qty);
            steps[4].OrderId.ShouldBe(originalSteps[4].OrderId);
            steps[4].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);
            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        [Theory]
        [MemberData(nameof(GetGrid2StepAwaitingSellData))]
        public async void UpdateNormalSteps_AwaitingSell_Change(decimal lowerPrice, decimal upperPrice,
            decimal[] buyPrices, decimal[] sellPrices, decimal[] qty)
        {
            // Arrange
            var grid = await _context.SpotGrids.FirstAsync(x => x.Id == _spotGridCreated.Id);
            var normalSteps = await _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToListAsync();
            for (var i = 0; i < 2; i++)
            {
                normalSteps[i].Status = SpotGridStepStatus.AwaitingSell;
                grid.QuoteBalance = (grid.QuoteBalance - normalSteps[i].Qty * normalSteps[i].BuyPrice).FixedNumber();
            }

            await _context.SaveChangesAsync(default);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = 40,
                NumberOfGrids = _spotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = _spotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(buyPrices[0]);
            steps[0].SellPrice.ShouldBe(sellPrices[0]);
            steps[0].Qty.ShouldBe(qty[0]);
            steps[0].OrderId.ShouldBeNull();
            steps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[1].BuyPrice.ShouldBe(buyPrices[1]);
            steps[1].SellPrice.ShouldBe(sellPrices[1]);
            steps[1].Qty.ShouldBe(qty[1]);
            steps[1].OrderId.ShouldBeNull();
            steps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[2].BuyPrice.ShouldBe(buyPrices[2]);
            steps[2].SellPrice.ShouldBe(sellPrices[2]);
            steps[2].Qty.ShouldBe(qty[2]);
            steps[2].OrderId.ShouldBeNull();
            steps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[3].BuyPrice.ShouldBe(buyPrices[3]);
            steps[3].SellPrice.ShouldBe(sellPrices[3]);
            steps[3].Qty.ShouldBe(qty[3]);
            steps[3].OrderId.ShouldBeNull();
            steps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            steps[4].BuyPrice.ShouldBe(buyPrices[4]);
            steps[4].SellPrice.ShouldBe(sellPrices[4]);
            steps[4].Qty.ShouldBe(qty[4]);
            steps[4].OrderId.ShouldBeNull();
            steps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        [Fact]
        public async void UpdateNormalSteps_AwaitingSell_NotChange()
        {
            // Arrange
            var grid = await _context.SpotGrids.FirstAsync(x => x.Id == _spotGridCreated.Id);
            var normalSteps = await _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToListAsync();
            for (var i = 0; i < 2; i++)
            {
                normalSteps[i].Status = SpotGridStepStatus.AwaitingSell;
                grid.QuoteBalance = (grid.QuoteBalance - normalSteps[i].Qty * normalSteps[i].BuyPrice).FixedNumber();
            }

            await _context.SaveChangesAsync(default);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = _spotGridCreated.LowerPrice,
                UpperPrice = _spotGridCreated.UpperPrice,
                TriggerPrice = 40,
                NumberOfGrids = _spotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = _spotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();
            normalSteps = await _context.SpotGridSteps
                .Where(s => s.SpotGridId == _spotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToListAsync();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(normalSteps[0].BuyPrice);
            steps[0].SellPrice.ShouldBe(normalSteps[0].SellPrice);
            steps[0].Qty.ShouldBe(normalSteps[0].Qty);
            steps[0].OrderId.ShouldBeNull();
            steps[0].Status.ShouldBe(normalSteps[0].Status);

            steps[1].BuyPrice.ShouldBe(normalSteps[1].BuyPrice);
            steps[1].SellPrice.ShouldBe(normalSteps[1].SellPrice);
            steps[1].Qty.ShouldBe(normalSteps[1].Qty);
            steps[1].OrderId.ShouldBeNull();
            steps[1].Status.ShouldBe(normalSteps[1].Status);

            steps[2].BuyPrice.ShouldBe(normalSteps[2].BuyPrice);
            steps[2].SellPrice.ShouldBe(normalSteps[2].SellPrice);
            steps[2].Qty.ShouldBe(normalSteps[2].Qty);
            steps[2].OrderId.ShouldBeNull();
            steps[2].Status.ShouldBe(normalSteps[2].Status);

            steps[3].BuyPrice.ShouldBe(normalSteps[3].BuyPrice);
            steps[3].SellPrice.ShouldBe(normalSteps[3].SellPrice);
            steps[3].Qty.ShouldBe(normalSteps[3].Qty);
            steps[3].OrderId.ShouldBeNull();
            steps[3].Status.ShouldBe(normalSteps[3].Status);

            steps[4].BuyPrice.ShouldBe(normalSteps[4].BuyPrice);
            steps[4].SellPrice.ShouldBe(normalSteps[4].SellPrice);
            steps[4].Qty.ShouldBe(normalSteps[4].Qty);
            steps[4].OrderId.ShouldBeNull();
            steps[4].Status.ShouldBe(normalSteps[4].Status);

            _kuCoinServiceMock.Verify(
                mock => mock.CancelOrder(
                    It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }
    }
}