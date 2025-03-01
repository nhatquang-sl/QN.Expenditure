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

            _spotGridCreated = _sender.Send(new CreateSpotGridCommand("BTCUSDT", 60, 70, 50, 10,
                SpotGridMode.ARITHMETIC, 100,
                110, 30)).Result;
        }

        [Fact]
        public async void Success()
        {
            // Arrange
            var resCreate = _spotGridCreated;

            // Act
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = resCreate.Id,
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
            var res = await _sender.Send(new UpdateSpotGridCommand
            {
                Id = _spotGridCreated.Id,
                LowerPrice = lowerPrice,
                UpperPrice = upperPrice,
                TriggerPrice = 40,
                NumberOfGrids = 5,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = 100
            });

            // Assert
            var entity = _context.SpotGrids
                .FirstOrDefault(x => x.UserId == _currentUser.Id && x.Id == res.Id);
            entity.ShouldNotBeNull();

            var steps = _context.SpotGridSteps.Where(s => s.SpotGridId == entity.Id)
                .OrderBy(s => s.BuyPrice).ToList();

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
    }
}