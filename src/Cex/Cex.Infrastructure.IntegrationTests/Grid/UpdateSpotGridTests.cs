using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.CreateSpotGrid;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using MediatR;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid
{
    public class UpdateSpotGridTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly ISender _sender;

        public UpdateSpotGridTests()
        {
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async void Success()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            _context.SpotGrids.Add(new SpotGrid());
            await _context.SaveChangesAsync(default);
            var updatedAt = DateTime.UtcNow.AddHours(1).ToUnixTimestampMilliseconds().ToDateTimeFromMilliseconds();

            // Act
            var resCreate =
                await _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100,
                    110, 30));
            var res = await _sender.Send(new UpdateSpotGridCommand(resCreate.Id, 50, 60, 40, 9, SpotGridMode.GEOMETRIC,
                90));

            // Assert
            res.ShouldNotBeNull();
            res.Id.ShouldBeGreaterThan(0);
            res.UserId.ShouldBe(_currentUser.Id);
            res.Symbol.ShouldBe("BTCUSDT");
            res.CreatedAt.ShouldBeGreaterThan(createdAt);
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
    }
}