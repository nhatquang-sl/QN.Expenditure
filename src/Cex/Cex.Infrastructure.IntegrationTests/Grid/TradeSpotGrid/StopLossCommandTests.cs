using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.TradeSpotGrid;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
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
    public class StopLossCommandTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly Mock<IKuCoinService> _kuCoinServiceMock = new();
        private readonly ISender _sender;

        public StopLossCommandTests()
        {
            _kuCoinServiceMock.Setup(x =>
                    x.PlaceOrder(
                        It.Is<PlaceOrderRequest>(order =>
                            order.Side == "sell" && order.Type == "market" && order.Symbol == CreateCommand.Symbol &&
                            order.Size == "2.9"),
                        It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_new_order_id_29");

            _kuCoinServiceMock.Setup(x =>
                    x.PlaceOrder(
                        It.Is<PlaceOrderRequest>(order =>
                            order.Side == "sell" && order.Type == "market" && order.Symbol == CreateCommand.Symbol &&
                            order.Size == "4.3"),
                        It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_new_order_id_30");

            _kuCoinServiceMock.Setup(x =>
                    x.GetOrderDetails(It.Is<string>(o => o == "fake_new_order_id_29")
                        , It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "fake_new_order_id_29",
                    Type = "market",
                    Side = "sell",
                    Size = "2.9",
                    Price = "29",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });

            _kuCoinServiceMock.Setup(x =>
                    x.GetOrderDetails(It.Is<string>(o => o == "fake_new_order_id_30")
                        , It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "fake_new_order_id_30",
                    Type = "market",
                    Side = "sell",
                    Size = "4.3",
                    Price = "30",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });

            ServiceCollection.AddSingleton(_kuCoinServiceMock.Object);

            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
        }

        /// <summary>
        ///     Stop Loss Step is triggered
        ///     Expect:
        ///     - KuCoinService.CancelOrder called twice
        ///     - KucCoinService.PlaceOrder called once
        ///     - KuCoinService.GetOrderDetails called once
        ///     - Grid status should be STOP_LOSS
        ///     - StopLoss step order id is null
        ///     - StopLoss step order quantity is base balance
        /// </summary>
        [Theory]
        [InlineData(29, 2.9, "fake_new_order_id_29")]
        [InlineData(30, 4.3, "fake_new_order_id_30")]
        public async Task Handle_Should_ExecuteStopLossLogic_When_StopLossTriggered(decimal lowestPrice,
            decimal baseBalance, string orderId)
        {
            // Arrange
            var oGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var oNormalSteps = oGrid.GridSteps.Where(s => s.Type == SpotGridStepType.Normal)
                .OrderBy(x => x.BuyPrice)
                .ToList();
            oGrid.BaseBalance = baseBalance;
            oNormalSteps[0].OrderId = "fake_order_id_1";
            oNormalSteps[1].OrderId = "fake_order_id_2";
            await _context.SaveChangesAsync(default);

            // Act
            await _sender.Send(new StopLossCommand(oGrid, new Kline { LowestPrice = lowestPrice }));

            // Assert:
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var stopLossStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.StopLoss);

            // 1. Verify CancelOrder is called for the canceled step.
            _kuCoinServiceMock.Verify(s => s.CancelOrder("fake_order_id_1", It.IsAny<KuCoinConfig>()), Times.Once);
            _kuCoinServiceMock.Verify(s => s.CancelOrder("fake_order_id_2", It.IsAny<KuCoinConfig>()), Times.Once);

            // 2. Verify PlaceOrder and GetOrderDetails are called.
            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.Is<PlaceOrderRequest>(req =>
                req.Symbol == grid.Symbol &&
                req.Side == "sell" &&
                req.Type == "market" &&
                req.Size == baseBalance.ToString(CultureInfo.InvariantCulture)
            ), It.IsAny<KuCoinConfig>()), Times.Once);
            _kuCoinServiceMock.Verify(s =>
                s.GetOrderDetails(orderId, It.IsAny<KuCoinConfig>()), Times.Once);

            // 3. Check that grid.Status is updated to STOP_LOSS.
            grid.Status.ShouldBe(SpotGridStatus.STOP_LOSS);

            // 4. The cancel order should have cleared the previous order id
            stopLossStep.OrderId.ShouldBeNull();
            stopLossStep.Status.ShouldBe(SpotGridStepStatus.SellOrderPlaced);

            // 5. Check that a new order was added.
            stopLossStep.Orders.ShouldHaveSingleItem();
            var createdOrder = stopLossStep.Orders.First();
            createdOrder.OrderId.ShouldBe(orderId);
            createdOrder.Price.ShouldBe(lowestPrice);
            createdOrder.OrigQty.ShouldBe(baseBalance);
        }

        /// <summary>
        ///     Stop Loss is null
        ///     Expect:
        ///     - Grid status should NOT be STOP_LOSS
        ///     - StopLoss step should be null
        ///     - KuCoinService.CancelOrder not called
        ///     - KucCoinService.PlaceOrder not called
        ///     - KuCoinService.GetOrderDetails not called
        /// </summary>
        [Fact]
        public async Task Handle_Should_NotUpdate_When_StopLossIsNull()
        {
            // Arrange
            await _sender.Send(new UpdateSpotGridCommand
            {
                Id = SpotGridCreated.Id,
                LowerPrice = CreateCommand.LowerPrice,
                UpperPrice = CreateCommand.UpperPrice,
                TriggerPrice = CreateCommand.TriggerPrice,
                NumberOfGrids = CreateCommand.NumberOfGrids,
                GridMode = SpotGridMode.GEOMETRIC,
                Investment = CreateCommand.Investment,
                StopLoss = null
            });
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);

            // Act
            await _sender.Send(new StopLossCommand(originalGrid, new Kline()));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var stopLossStep = _context.SpotGridSteps.FirstOrDefault(step =>
                step.SpotGridId == grid.Id && step.Type == SpotGridStepType.StopLoss);

            grid.Status.ShouldNotBe(SpotGridStatus.STOP_LOSS);

            stopLossStep.ShouldBeNull();

            // KuCoin services are not invoked.
            _kuCoinServiceMock.Verify(s => s.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()), Times.Never);
            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.IsAny<PlaceOrderRequest>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
            _kuCoinServiceMock.Verify(s => s.GetOrderDetails(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Stop Loss price is not reached
        ///     Expect:
        ///     - Grid status should NOT be STOP_LOSS
        ///     - KuCoinService.CancelOrder not called
        ///     - KucCoinService.PlaceOrder not called
        ///     - KuCoinService.GetOrderDetails not called
        /// </summary>
        [Theory]
        [InlineData(31)]
        [InlineData(32)]
        public async Task Handle_Should_NotUpdate_When_StopLossNotMatch(decimal lowestPrice)
        {
            // Arrange
            var originalGrid = _context.SpotGrids
                .First(x => x.Id == SpotGridCreated.Id);
            var kline = new Kline { LowestPrice = lowestPrice };

            // Act
            await _sender.Send(new StopLossCommand(originalGrid, kline));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            grid.Status.ShouldNotBe(SpotGridStatus.STOP_LOSS);

            // KuCoin services are not invoked.
            _kuCoinServiceMock.Verify(s => s.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()), Times.Never);
            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.IsAny<PlaceOrderRequest>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
            _kuCoinServiceMock.Verify(s => s.GetOrderDetails(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }
    }
}