using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Domain.Entities;
using FluentValidation.TestHelper;

namespace Cex.Infrastructure.IntegrationTests.Grid.UpdateSpotGrid
{
    public class ValidatorTests
    {
        private readonly UpdateSpotGridCommandValidator _validator = new();

        [Fact]
        public async Task Validate_ValidCommand_ShouldSucceed()
        {
            // Arrange - valid input is provided.
            var command = new UpdateSpotGridCommand
            {
                Id = 1,
                LowerPrice = 50,
                UpperPrice = 100,
                TriggerPrice = 75,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.ARITHMETIC,
                Investment = 1000,
                TakeProfit = 120,
                StopLoss = 40
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_InvalidLowerAndUpperPrice_ShouldFail()
        {
            // Arrange - lower price exceeds upper price.
            var command = new UpdateSpotGridCommand
            {
                Id = 1,
                LowerPrice = 120,
                UpperPrice = 100,
                TriggerPrice = 75,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.ARITHMETIC,
                Investment = 1000
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert - expect error on UpperPrice.
            result.ShouldHaveValidationErrorFor(x => x.UpperPrice);
        }

        [Fact]
        public async Task Validate_InvalidTakeProfitAndStopLoss_ShouldFail()
        {
            // Arrange - take profit is not greater than stop loss.
            var command = new UpdateSpotGridCommand
            {
                Id = 1,
                LowerPrice = 50,
                UpperPrice = 100,
                TriggerPrice = 75,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.ARITHMETIC,
                Investment = 1000,
                TakeProfit = 30,
                StopLoss = 40
            };

            // Act
            var result = await _validator.TestValidateAsync(command);

            // Assert - expect validation error for TakeProfit.
            result.ShouldHaveValidationErrorFor(x => x.TakeProfit);
        }
    }
}