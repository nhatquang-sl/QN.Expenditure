using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Domain.Entities;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid.UpdateSpotGrid
{
    // Initial Step Placed
    // Initial Step Matched
    public class GridNewStateTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ISender _sender;

        public GridNewStateTests()
        {
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
        }

        /// <summary>
        ///     Update Investment
        ///     Expect:
        ///     Initial Step should be changed:
        ///     1. Quantity based on Investment
        ///     2. Status should be AwaitingBuy
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(150, 0.75)]
        [InlineData(200, 1)]
        public async void UpdateInvestment_ChangeInitialStep(decimal investment, decimal triggerQty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = investment
            });

            // Assert
            var initStepUpdated = _context.SpotGridSteps
                .First(x => x.SpotGridId == res.Id && x.Type == SpotGridStepType.Initial);
            initStepUpdated.ShouldNotBeNull();
            initStepUpdated.BuyPrice.ShouldBe(CreateCommand.TriggerPrice);
            initStepUpdated.SellPrice.ShouldBe(CreateCommand.TriggerPrice);
            initStepUpdated.Qty.ShouldBe(triggerQty);
            initStepUpdated.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            initStepUpdated.OrderId.ShouldBeNull();
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Investment
        ///     Expect:
        ///     Normals Steps should be change Quantity based on Investment
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [MemberData(nameof(GetNormalStepsBaseOnInvestment))]
        public async void UpdateInvestment_ChangeNormalSteps(decimal investment, decimal[] buyPrices,
            decimal[] sellPrices, decimal[] qty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = investment
            });

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
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Investment
        ///     Expect:
        ///     Quote Balance should be changed
        /// </summary>
        [Theory]
        [InlineData(90, 110, 100)]
        [InlineData(85, 120, 105)]
        [InlineData(55, 130, 85)]
        public async void UpdateInvestment_ChangeQuoteBalance(decimal currentQuoteBalance, decimal investment,
            decimal quoteBalance)
        {
            // Arrange
            var originalGrid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            originalGrid.QuoteBalance = currentQuoteBalance;
            await _context.SaveChangesAsync(default);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = investment
            });

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            grid.QuoteBalance.ShouldBe(quoteBalance);
        }

        /// <summary>
        ///     Update Lower/Upper Price
        ///     Expect:
        ///     Normals Steps should be changed:
        ///     1. Buy/Sell Price
        ///     2. Quantity
        ///     3. Status = AwaitingBuy
        ///     4. OrderId should be null
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [MemberData(nameof(GetGridStepTestData))]
        public async void UpdateLowerUpperPrice_ChangeNormalSteps(decimal lowerPrice, decimal upperPrice,
            decimal[] buyPrices, decimal[] sellPrices, decimal[] qty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = SpotGridCreated.TriggerPrice,
                NumberOfGrids = SpotGridCreated.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = SpotGridCreated.Investment
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
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Lower/Upper Price
        ///     Expect:
        ///     Initial Step should be un-changed
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(90, 110)]
        [InlineData(85, 120)]
        [InlineData(55, 130)]
        public async void UpdateLowerUpperPrice_UnChangeInitialStep(decimal lowerPrice, decimal upperPrice)
        {
            // Arrange
            var originalInitialStep = _context.SpotGridSteps
                .First(s => s.SpotGridId == SpotGridCreated.Id && s.Type == SpotGridStepType.Initial);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var initialStep = _context.SpotGridSteps
                .First(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Initial);

            initialStep.ShouldNotBeNull();
            initialStep.BuyPrice.ShouldBe(originalInitialStep.BuyPrice);
            initialStep.SellPrice.ShouldBe(originalInitialStep.SellPrice);
            initialStep.Qty.ShouldBe(originalInitialStep.Qty);
            initialStep.OrderId.ShouldBe(originalInitialStep.OrderId);
            initialStep.Status.ShouldBe(originalInitialStep.Status);
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Lower/Upper Price
        ///     Expect:
        ///     Quote Balance should be un-changed
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(90, 110)]
        [InlineData(85, 120)]
        [InlineData(55, 130)]
        public async void UpdateLowerUpperPrice_UnChangeQuoteBalance(decimal lowerPrice, decimal upperPrice)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            grid.QuoteBalance.ShouldBe(SpotGridCreated.QuoteBalance);
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Lower/Upper Price
        ///     Expect:
        ///     Normals Steps should be changed:
        ///     1. Buy/Sell Price
        ///     2. Quantity
        ///     3. Status = AwaitingBuy
        ///     4. OrderId should be null
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [MemberData(nameof(GetNormalStepsBaseOnNumOfGrids))]
        public async void UpdateNumOfGrids_ChangeNormalSteps(int numOfGrids,
            decimal[] buyPrices, decimal[] sellPrices, decimal[] qty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = SpotGridCreated.LowerPrice,
                UpperPrice = SpotGridCreated.UpperPrice,
                TriggerPrice = SpotGridCreated.TriggerPrice,
                NumberOfGrids = numOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = SpotGridCreated.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(numOfGrids);
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

            if (numOfGrids < 3)
            {
                return;
            }

            steps[2].BuyPrice.ShouldBe(buyPrices[2]);
            steps[2].SellPrice.ShouldBe(sellPrices[2]);
            steps[2].Qty.ShouldBe(qty[2]);
            steps[2].OrderId.ShouldBeNull();
            steps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            if (numOfGrids < 4)
            {
                return;
            }

            steps[3].BuyPrice.ShouldBe(buyPrices[3]);
            steps[3].SellPrice.ShouldBe(sellPrices[3]);
            steps[3].Qty.ShouldBe(qty[3]);
            steps[3].OrderId.ShouldBeNull();
            steps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);

            if (numOfGrids < 5)
            {
                return;
            }

            steps[4].BuyPrice.ShouldBe(buyPrices[4]);
            steps[4].SellPrice.ShouldBe(sellPrices[4]);
            steps[4].Qty.ShouldBe(qty[4]);
            steps[4].OrderId.ShouldBeNull();
            steps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Number Of Grids
        ///     Expect:
        ///     Initial Step should be un-changed
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async void UpdateNumOfGrids_UnChangeInitialStep(int numOfGrids)
        {
            // Arrange
            var originalInitialStep = _context.SpotGridSteps
                .First(s => s.SpotGridId == SpotGridCreated.Id && s.Type == SpotGridStepType.Initial);

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = numOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var initialStep = _context.SpotGridSteps
                .First(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Initial);

            initialStep.ShouldNotBeNull();
            initialStep.BuyPrice.ShouldBe(originalInitialStep.BuyPrice);
            initialStep.SellPrice.ShouldBe(originalInitialStep.SellPrice);
            initialStep.Qty.ShouldBe(originalInitialStep.Qty);
            initialStep.OrderId.ShouldBe(originalInitialStep.OrderId);
            initialStep.Status.ShouldBe(originalInitialStep.Status);
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Number Of Grids
        ///     Expect:
        ///     Quote Balance should be un-changed
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async void UpdateNumOfGrids_UnChangeQuoteBalance(int numOfGrids)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = numOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            grid.QuoteBalance.ShouldBe(SpotGridCreated.QuoteBalance);

            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Trigger Price
        ///     Expect:
        ///     Initial Step should be changed:
        ///     1. Buy/Sell Price
        ///     2. Quantity based on Investment
        ///     3. Status should be AwaitingBuy
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(30, 0.8333)]
        [InlineData(40, 0.625)]
        [InlineData(60, 0.4166)]
        public async void UpdateTriggerPrice_ChangeInitialStep(decimal triggerPrice, decimal triggerQty)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = triggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var initStepUpdated = _context.SpotGridSteps
                .First(x => x.SpotGridId == res.Id && x.Type == SpotGridStepType.Initial);
            initStepUpdated.ShouldNotBeNull();
            initStepUpdated.BuyPrice.ShouldBe(triggerPrice);
            initStepUpdated.SellPrice.ShouldBe(triggerPrice);
            initStepUpdated.Qty.ShouldBe(triggerQty);
            initStepUpdated.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Trigger Price
        ///     Expect:
        ///     Normal Steps should be un-changed
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(30)]
        [InlineData(40)]
        [InlineData(60)]
        public async void UpdateTriggerPrice_UnChangeNormalSteps(decimal triggerPrice)
        {
            // Arrange
            var normalSteps = await _context.SpotGridSteps
                .Where(s => s.SpotGridId == SpotGridCreated.Id
                            && s.Type == SpotGridStepType.Normal)
                .ToListAsync();

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = triggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var steps = _context.SpotGridSteps
                .Where(s => s.SpotGridId == res.Id && s.Type == SpotGridStepType.Normal)
                .ToList()
                .OrderBy(s => s.BuyPrice).ToList();

            steps.ShouldNotBeNull();
            steps.Count.ShouldBe(5);
            steps[0].BuyPrice.ShouldBe(normalSteps[0].BuyPrice);
            steps[0].SellPrice.ShouldBe(normalSteps[0].SellPrice);
            steps[0].Qty.ShouldBe(normalSteps[0].Qty);
            steps[0].OrderId.ShouldBe(normalSteps[0].OrderId);
            steps[0].Status.ShouldBe(normalSteps[0].Status);

            steps[1].BuyPrice.ShouldBe(normalSteps[1].BuyPrice);
            steps[1].SellPrice.ShouldBe(normalSteps[1].SellPrice);
            steps[1].Qty.ShouldBe(normalSteps[1].Qty);
            steps[1].OrderId.ShouldBe(normalSteps[1].OrderId);
            steps[1].Status.ShouldBe(normalSteps[1].Status);

            steps[2].BuyPrice.ShouldBe(normalSteps[2].BuyPrice);
            steps[2].SellPrice.ShouldBe(normalSteps[2].SellPrice);
            steps[2].Qty.ShouldBe(normalSteps[2].Qty);
            steps[2].OrderId.ShouldBe(normalSteps[2].OrderId);
            steps[2].Status.ShouldBe(normalSteps[2].Status);

            steps[3].BuyPrice.ShouldBe(normalSteps[3].BuyPrice);
            steps[3].SellPrice.ShouldBe(normalSteps[3].SellPrice);
            steps[3].Qty.ShouldBe(normalSteps[3].Qty);
            steps[3].OrderId.ShouldBe(normalSteps[3].OrderId);
            steps[3].Status.ShouldBe(normalSteps[3].Status);

            steps[4].BuyPrice.ShouldBe(normalSteps[4].BuyPrice);
            steps[4].SellPrice.ShouldBe(normalSteps[4].SellPrice);
            steps[4].Qty.ShouldBe(normalSteps[4].Qty);
            steps[4].OrderId.ShouldBe(normalSteps[4].OrderId);
            steps[4].Status.ShouldBe(normalSteps[4].Status);

            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(),
                    It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Update Trigger Price
        ///     Expect:
        ///     Quote Balance should be un-changed
        ///     Don't call KuCoin Api
        /// </summary>
        [Theory]
        [InlineData(110)]
        [InlineData(120)]
        public async void UpdateTriggerPrice_UnChangeQuoteBalance(decimal triggerPrice)
        {
            // Arrange

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = triggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment
            });

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            grid.QuoteBalance.ShouldBe(SpotGridCreated.QuoteBalance);

            KuCoinServiceMock.Verify(
                mock => mock.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }
    }
}