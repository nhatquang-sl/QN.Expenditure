using Application.BnbSpotGrid.Commands.CreateSpotGrid;
using Application.BnbSpotGrid.Commands.UpdateSpotGrid;
using Application.Common.Abstractions;
using Application.Common.Extensions;
using Domain.Entities;
using MediatR;
using Shouldly;

namespace Infrastructure.IntegrationTests.BnbSpotGrid
{
    public class UpdateSpotGridTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IApplicationDbContext _context;

        public UpdateSpotGridTests() : base()
        {
            _sender = GetService<ISender>();
            _context = GetService<IApplicationDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async void Success()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var updatedAt = DateTime.UtcNow.AddHours(1).ToUnixTimestampMilliseconds().ToDateTimeFromMilliseconds();

            // Act
            var resCreate = await _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 10, SpotGridMode.ARITHMETIC, 100, 110, 30));
            var res = await _sender.Send(new UpdateSpotGridCommand(resCreate.Id, 50, 60, 40, 9, SpotGridMode.GEOMETRIC, 90, 100, 20));

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
            res.TakeProfit.ShouldBe(100);
            res.StopLoss.ShouldBe(20);
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
    }
}
