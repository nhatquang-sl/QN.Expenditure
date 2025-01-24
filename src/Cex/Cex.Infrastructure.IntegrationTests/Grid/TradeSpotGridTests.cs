using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.TradeSpotGrid;
using Cex.Domain.Entities;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid
{
    public class TradeSpotGridTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;

        private readonly List<OrderDetails> _orderDetails = DataGenerator.KuCoinOrderDetails();

        private readonly ISender _sender;

        public TradeSpotGridTests()
        {
            Mock<IKuCoinService> kuCoinServiceMock = new();
            kuCoinServiceMock
                .Setup(service => service.GetKlines("BTC-USDT", It.IsAny<string>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync([
                    new Kline
                    {
                        LowestPrice = DataGenerator.BtcLowestPrice,
                        HighestPrice = DataGenerator.BtcHighestPrice
                    }
                ]);

            kuCoinServiceMock
                .Setup(service => service.GetKlines("ETH-USDT", It.IsAny<string>(),
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync([
                    new Kline
                    {
                        LowestPrice = 30
                    }
                ]);

            kuCoinServiceMock
                .Setup(x => x.PlaceOrder(It.IsAny<OrderRequest>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync((OrderRequest orderRequest, KuCoinConfig _) =>
                    _orderDetails.First(o => o.Side == orderRequest.Side && o.Price == orderRequest.Price).Id
                );

            kuCoinServiceMock
                .Setup(x => x.GetOrderDetails(It.IsAny<string>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync((string orderId, KuCoinConfig _) => _orderDetails.First(o => o.Id == orderId));

            ServiceCollection.AddSingleton(kuCoinServiceMock.Object);
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
        }

        [Theory]
        [InlineData(100)]
        [InlineData(99)]
        [InlineData(98)]
        public async void StatusFromNewToRunning_Success(decimal triggerPrice)
        {
            // Arrange
            _context.SpotGrids.Add(DataGenerator.NewSpotGrid(1, "BTC-USDT", triggerPrice));
            _context.SpotGrids.Add(DataGenerator.NewSpotGrid(2, "ETH-USDT", 31));
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new TradeSpotGridCommand());

            // Assert
            var res = await _context.SpotGrids.ToListAsync();
            res.ShouldNotBeNull();
            res.Count.ShouldBe(2);
            res.First(x => x.Symbol == "BTC-USDT").Status.ShouldBe(SpotGridStatus.RUNNING);
            res.First(x => x.Symbol == "ETH-USDT").Status.ShouldBe(SpotGridStatus.NEW);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(102)]
        [InlineData(103)]
        public async void StatusFromNewToRunning_Invalid(decimal triggerPrice)
        {
            // Arrange
            _context.SpotGrids.Add(DataGenerator.NewSpotGrid(1, "BTC-USDT", triggerPrice));
            _context.SpotGrids.Add(DataGenerator.NewSpotGrid(2, "ETH-USDT", 31));
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new TradeSpotGridCommand());

            // Assert
            var res = await _context.SpotGrids.ToListAsync();
            res.ShouldNotBeNull();
            res.Count.ShouldBe(2);
            res.First(x => x.Symbol == "BTC-USDT").Status.ShouldBe(SpotGridStatus.NEW);
            res.First(x => x.Symbol == "ETH-USDT").Status.ShouldBe(SpotGridStatus.NEW);
        }

        [Fact]
        public async void StepStatusToBuyOrderPlaced_Success()
        {
            // Arrange
            var grid = DataGenerator.NewSpotGrid(1, "BTC-USDT", 100);
            grid.GridSteps.Add(DataGenerator.GridStep(1, 101, 102, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(2, 100, 101, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(3, 99, 100, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(4, 98, 99, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(5, 97, 98, SpotGridStepStatus.AwaitingBuy));
            _context.SpotGrids.Add(grid);
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new TradeSpotGridCommand());

            // Assert
            var gridSteps = await _context.SpotGridSteps.ToArrayAsync();
            gridSteps.Length.ShouldBe(5);
            gridSteps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            gridSteps[1].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);
            gridSteps[1].OrderId.ShouldBe(_orderDetails[0].Id);

            gridSteps[2].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);
            gridSteps[2].OrderId.ShouldBe(_orderDetails[1].Id);

            gridSteps[3].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);
            gridSteps[3].OrderId.ShouldBe(_orderDetails[2].Id);

            gridSteps[4].Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);
            gridSteps[4].OrderId.ShouldBe(_orderDetails[3].Id);
        }

        [Fact]
        public async void StepStatusToAwaitingSell_Success()
        {
            // Arrange
            var grid = DataGenerator.NewSpotGrid(1, "BTC-USDT", 100);
            grid.GridSteps.Add(DataGenerator.GridStep(1, 101, 102, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(2, 100, 101, SpotGridStepStatus.BuyOrderPlaced,
                _orderDetails[0].Id));
            grid.GridSteps.Add(DataGenerator.GridStep(3, 99, 100, SpotGridStepStatus.BuyOrderPlaced,
                _orderDetails[1].Id));
            grid.GridSteps.Add(
                DataGenerator.GridStep(4, 98, 99, SpotGridStepStatus.BuyOrderPlaced, _orderDetails[2].Id));
            grid.GridSteps.Add(
                DataGenerator.GridStep(5, 97, 98, SpotGridStepStatus.BuyOrderPlaced, _orderDetails[3].Id));
            _context.SpotGrids.Add(grid);
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new TradeSpotGridCommand());

            // Assert
            var gridSteps = await _context.SpotGridSteps.ToArrayAsync();
            gridSteps.Length.ShouldBe(5);
            gridSteps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            gridSteps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[1].OrderId.ShouldBeNull();
            gridSteps[1].Orders.Count.ShouldBe(1);
            AssertOrder(gridSteps[1].Orders.ElementAt(0), grid, _orderDetails[0]);

            gridSteps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[2].OrderId.ShouldBeNull();
            gridSteps[2].Orders.Count.ShouldBe(1);
            AssertOrder(gridSteps[2].Orders.ElementAt(0), grid, _orderDetails[1]);

            gridSteps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[3].OrderId.ShouldBeNull();
            gridSteps[3].Orders.Count.ShouldBe(1);
            AssertOrder(gridSteps[3].Orders.ElementAt(0), grid, _orderDetails[2]);

            gridSteps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[4].OrderId.ShouldBeNull();
            gridSteps[4].Orders.Count.ShouldBe(1);
            AssertOrder(gridSteps[4].Orders.ElementAt(0), grid, _orderDetails[3]);
        }

        [Fact]
        public async void StepStatusToSellOrderPlaced_Success()
        {
            // Arrange
            var grid = DataGenerator.NewSpotGrid(1, "BTC-USDT", 100);
            grid.GridSteps.Add(DataGenerator.GridStep(1, 101, 112, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(2, 100, 111, SpotGridStepStatus.BuyOrderPlaced,
                _orderDetails[0].Id));
            grid.GridSteps.Add(DataGenerator.GridStep(3, 99, 110, SpotGridStepStatus.BuyOrderPlaced,
                _orderDetails[1].Id));
            grid.GridSteps.Add(
                DataGenerator.GridStep(4, 98, 109, SpotGridStepStatus.BuyOrderPlaced, _orderDetails[2].Id));
            grid.GridSteps.Add(
                DataGenerator.GridStep(5, 97, 108, SpotGridStepStatus.BuyOrderPlaced, _orderDetails[3].Id));
            _context.SpotGrids.Add(grid);
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new TradeSpotGridCommand());
            await _sender.Send(new TradeSpotGridCommand());

            // Assert
            var gridSteps = await _context.SpotGridSteps.ToArrayAsync();
            gridSteps.Length.ShouldBe(5);
            gridSteps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            gridSteps[1].Status.ShouldBe(SpotGridStepStatus.SellOrderPlaced);
            gridSteps[1].OrderId.ShouldBe(_orderDetails[4].Id);

            gridSteps[2].Status.ShouldBe(SpotGridStepStatus.SellOrderPlaced);
            gridSteps[2].OrderId.ShouldBe(_orderDetails[5].Id);

            gridSteps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[3].OrderId.ShouldBeNull();

            gridSteps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[4].OrderId.ShouldBeNull();
        }

        [Fact]
        public async void StepStatusToAwaitingBuy_Success()
        {
            // Arrange
            var grid = DataGenerator.NewSpotGrid(1, "BTC-USDT", 100);
            grid.GridSteps.Add(DataGenerator.GridStep(1, 101, 112, SpotGridStepStatus.AwaitingBuy));
            grid.GridSteps.Add(DataGenerator.GridStep(2, 100, 111, SpotGridStepStatus.BuyOrderPlaced,
                _orderDetails[0].Id));
            grid.GridSteps.Add(DataGenerator.GridStep(3, 99, 110, SpotGridStepStatus.BuyOrderPlaced,
                _orderDetails[1].Id));
            grid.GridSteps.Add(
                DataGenerator.GridStep(4, 98, 109, SpotGridStepStatus.BuyOrderPlaced, _orderDetails[2].Id));
            grid.GridSteps.Add(
                DataGenerator.GridStep(5, 97, 108, SpotGridStepStatus.BuyOrderPlaced, _orderDetails[3].Id));
            _context.SpotGrids.Add(grid);
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new TradeSpotGridCommand());
            await _sender.Send(new TradeSpotGridCommand());
            await _sender.Send(new TradeSpotGridCommand());

            // Assert
            var gridSteps = await _context.SpotGridSteps.ToArrayAsync();
            gridSteps.Length.ShouldBe(5);
            gridSteps[0].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            gridSteps[1].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            gridSteps[1].OrderId.ShouldBeNull();

            gridSteps[2].Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            gridSteps[2].OrderId.ShouldBeNull();

            gridSteps[3].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[3].OrderId.ShouldBeNull();

            gridSteps[4].Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            gridSteps[4].OrderId.ShouldBeNull();
        }

        private static void AssertOrder(SpotOrder order, SpotGrid grid, OrderDetails orderDetails)
        {
            order.UserId.ShouldBe(grid.UserId);
            order.Symbol.ShouldBe(grid.Symbol);
            order.OrderId.ShouldBe(orderDetails.Id);
            order.ClientOrderId.ShouldBe(orderDetails.ClientOid);
            order.Price.ShouldBe(decimal.Parse(orderDetails.Price));
            order.OrigQty.ShouldBe(decimal.Parse(orderDetails.Size));
            order.TimeInForce.ShouldBe(orderDetails.TimeInForce);
            order.Type.ShouldBe(orderDetails.Type);
            order.Side.ShouldBe(orderDetails.Side);
            order.Fee.ShouldBe(decimal.Parse(orderDetails.Fee));
            order.FeeCurrency.ShouldBe(orderDetails.FeeCurrency);
            order.CreatedAt
                .ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(orderDetails.CreatedAt).UtcDateTime);
        }
    }
}