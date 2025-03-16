using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Exceptions;
using MediatR;

namespace Cex.Infrastructure.IntegrationTests.Grid.UpdateSpotGrid
{
    public class CommandTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ISender _sender;

        public CommandTests()
        {
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
        }

        [Fact]
        public async Task UpdateCommand_Should_UpdateGridSuccessfully()
        {
            // Arrange - update trigger price.
            var newTriggerPrice = SpotGridCreated.TriggerPrice + 5;
            var command = new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = SpotGridCreated.LowerPrice,
                UpperPrice = SpotGridCreated.UpperPrice,
                TriggerPrice = newTriggerPrice,
                NumberOfGrids = SpotGridCreated.NumberOfGrids,
                GridMode = SpotGridCreated.GridMode,
                Investment = SpotGridCreated.Investment
            };

            // Act
            var result = await _sender.Send(command, CancellationToken.None);

            // Assert
            Assert.Equal(newTriggerPrice, result.TriggerPrice);
        }

        [Fact]
        public async Task UpdateCommand_Should_ThrowNotFound_When_GridNotFound()
        {
            // Arrange - non-existent grid id.
            var command = new UpdateSpotGridCommand
            {
                Id = -1,
                LowerPrice = 50,
                UpperPrice = 80,
                TriggerPrice = 55,
                NumberOfGrids = 5,
                GridMode = SpotGridCreated.GridMode,
                Investment = 100
            };

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await _sender.Send(command, CancellationToken.None));
        }

        [Fact]
        public async Task UpdateCommand_Should_AddTakeProfitStep_When_NotExists()
        {
            // Arrange - supply a valid TakeProfit value while no take profit step exists.
            var command = new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = SpotGridCreated.LowerPrice,
                UpperPrice = SpotGridCreated.UpperPrice,
                TriggerPrice = SpotGridCreated.TriggerPrice,
                NumberOfGrids = SpotGridCreated.NumberOfGrids,
                GridMode = SpotGridCreated.GridMode,
                Investment = SpotGridCreated.Investment,
                TakeProfit = SpotGridCreated.UpperPrice + 20
            };

            // Act
            var result = await _sender.Send(command, CancellationToken.None);

            // Assert - verify a take profit step was added.
            var tpStep = _context.SpotGridSteps.FirstOrDefault(s =>
                s.SpotGridId == result.Id && s.Type == SpotGridStepType.TakeProfit);
            Assert.NotNull(tpStep);
            Assert.Equal(command.TakeProfit, tpStep.BuyPrice);
            Assert.Equal(command.TakeProfit, tpStep.SellPrice);
        }

        [Fact]
        public async Task UpdateCommand_Should_UpdateStopLossStep_When_Exists()
        {
            // Arrange - add a stop loss step manually.
            var grid = _context.SpotGrids.First(s => s.Id == SpotGridCreated.Id);
            var stopLossStep = new SpotGridStep
            {
                SpotGridId = grid.Id,
                Type = SpotGridStepType.StopLoss,
                BuyPrice = grid.LowerPrice - 10,
                SellPrice = grid.LowerPrice - 10,
                Qty = 1,
                OrderId = "order_sl",
                Status = SpotGridStepStatus.AwaitingBuy
            };
            _context.SpotGridSteps.Add(stopLossStep);
            await _context.SaveChangesAsync(CancellationToken.None);

            // Change the stop loss value.
            var newStopLoss = grid.LowerPrice - 5;
            var command = new UpdateSpotGridCommand
            {
                Id = grid.Id,
                LowerPrice = grid.LowerPrice,
                UpperPrice = grid.UpperPrice,
                TriggerPrice = grid.TriggerPrice,
                NumberOfGrids = grid.NumberOfGrids,
                GridMode = grid.GridMode,
                Investment = grid.Investment,
                StopLoss = newStopLoss
            };

            // Act
            var result = await _sender.Send(command, CancellationToken.None);

            // Assert - the existing stop loss step should update accordingly.
            var updatedSl = _context.SpotGridSteps
                .FirstOrDefault(s => s.SpotGridId == SpotGridCreated.Id && s.Type == SpotGridStepType.StopLoss);
            Assert.NotNull(updatedSl);
            // If the step was in AwaitingBuy status the OrderId is cleared.
            Assert.Null(updatedSl.OrderId);
        }
    }
}