using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.TradeSpotGrid;
using Cex.Application.Grid.Commands.UpdateSpotGrid;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.ExternalServices.KuCoin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.Grid.TradeSpotGrid
{
    public class TakeProfitCommandTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly Mock<IKuCoinService> _kuCoinServiceMock = new();
        private readonly ISender _sender;

        public TakeProfitCommandTests()
        {
            _kuCoinServiceMock.Setup(x =>
                    x.PlaceOrder(
                        It.Is<OrderRequest>(order =>
                            order.Side == "sell" && order.Type == "market" && order.Symbol == CreateCommand.Symbol &&
                            order.Size == "2.9"),
                        It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_new_order_id_2.9");

            _kuCoinServiceMock.Setup(x =>
                    x.PlaceOrder(
                        It.Is<OrderRequest>(order =>
                            order.Side == "sell" && order.Type == "market" && order.Symbol == CreateCommand.Symbol &&
                            order.Size == "4.3"),
                        It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_new_order_id_4.3");

            _kuCoinServiceMock.Setup(x =>
                    x.GetOrderDetails(It.Is<string>(o => o == "fake_new_order_id_2.9")
                        , It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "fake_new_order_id_2.9",
                    Type = "market",
                    Side = "sell",
                    Size = "2.9",
                    Price = "110",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });

            _kuCoinServiceMock.Setup(x =>
                    x.GetOrderDetails(It.Is<string>(o => o == "fake_new_order_id_4.3")
                        , It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "fake_new_order_id_4.3",
                    Type = "market",
                    Side = "sell",
                    Size = "4.3",
                    Price = "111",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });

            ServiceCollection.AddSingleton(_kuCoinServiceMock.Object);

            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
        }

        /// <summary>
        ///     Take Profit Step is triggered
        ///     Expect:
        ///     - KuCoinService.CancelOrder called twice
        ///     - KucCoinService.PlaceOrder called once
        ///     - KuCoinService.GetOrderDetails called once
        ///     - Grid status should be TAKE_PROFIT
        ///     - TakeProfit step order id is not null
        ///     - TakeProfit step order quantity is base balance
        /// </summary>
        [Theory]
        [InlineData(110, 2.9, "fake_new_order_id_2.9")]
        [InlineData(111, 4.3, "fake_new_order_id_4.3")]
        public async Task Handle_Should_ExecuteTakeProfitLogic_When_StopLossTriggered(decimal closePrice,
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
            await _sender.Send(new TakeProfitCommand(oGrid, new Kline { ClosePrice = closePrice }));

            // Assert:
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var takeProfitStep = grid.GridSteps.First(x => x.Type == SpotGridStepType.TakeProfit);

            // 1. Verify CancelOrder is called for the canceled step.
            _kuCoinServiceMock.Verify(s => s.CancelOrder("fake_order_id_1", It.IsAny<KuCoinConfig>()), Times.Once);
            _kuCoinServiceMock.Verify(s => s.CancelOrder("fake_order_id_2", It.IsAny<KuCoinConfig>()), Times.Once);

            // 2. Verify PlaceOrder and GetOrderDetails are called.
            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.Is<OrderRequest>(req =>
                req.Symbol == grid.Symbol &&
                req.Side == "sell" &&
                req.Type == "market" &&
                req.Size == baseBalance.ToString(CultureInfo.InvariantCulture)
            ), It.IsAny<KuCoinConfig>()), Times.Once);
            _kuCoinServiceMock.Verify(s =>
                s.GetOrderDetails(orderId, It.IsAny<KuCoinConfig>()), Times.Once);

            // 3. Check that grid.Status is updated to TAKE_PROFIT.
            grid.Status.ShouldBe(SpotGridStatus.TAKE_PROFIT);

            // 4. The cancel order should have cleared the previous order id
            takeProfitStep.OrderId.ShouldBe(orderId);
            takeProfitStep.Status.ShouldBe(SpotGridStepStatus.SellOrderPlaced);

            // 5. Check that a new order was added.
            takeProfitStep.Orders.ShouldHaveSingleItem();
            var createdOrder = takeProfitStep.Orders.First();
            createdOrder.OrderId.ShouldBe(orderId);
            createdOrder.Price.ShouldBe(closePrice);
            createdOrder.OrigQty.ShouldBe(baseBalance);
        }

        /// <summary>
        ///     Take Profit price is not reached
        ///     Expect:
        ///     - Grid status should NOT be TAKE_PROFIT
        ///     - KuCoinService.CancelOrder not called
        ///     - KucCoinService.PlaceOrder not called
        ///     - KuCoinService.GetOrderDetails not called
        /// </summary>
        [Theory]
        [InlineData(108)]
        [InlineData(109)]
        public async Task Handle_Should_NotUpdate_When_TakeProfitNotMatch(decimal closePrice)
        {
            // Arrange
            var originalGrid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var kline = new Kline { ClosePrice = closePrice };

            // Act
            await _sender.Send(new TakeProfitCommand(originalGrid, kline));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            grid.Status.ShouldNotBe(SpotGridStatus.TAKE_PROFIT);

            // KuCoin services are not invoked.
            _kuCoinServiceMock.Verify(s => s.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()), Times.Never);
            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.IsAny<OrderRequest>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
            _kuCoinServiceMock.Verify(s => s.GetOrderDetails(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }

        /// <summary>
        ///     Take Profit is null
        ///     Expect:
        ///     - Grid status should NOT be TAKE_PROFIT
        ///     - TakeProfit step should be null
        ///     - KuCoinService.CancelOrder not called
        ///     - KucCoinService.PlaceOrder not called
        ///     - KuCoinService.GetOrderDetails not called
        /// </summary>
        [Fact]
        public async Task Handle_Should_NotUpdate_When_TaskProfitIsNull()
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
                StopLoss = CreateCommand.StopLoss,
                TakeProfit = null
            });
            var originalGrid = _context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);

            // Act
            await _sender.Send(new TakeProfitCommand(originalGrid, new Kline()));

            // Assert
            var grid = _context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var takeProfitStep = _context.SpotGridSteps.FirstOrDefault(step =>
                step.SpotGridId == grid.Id && step.Type == SpotGridStepType.TakeProfit);

            grid.Status.ShouldNotBe(SpotGridStatus.TAKE_PROFIT);

            takeProfitStep.ShouldBeNull();

            // KuCoin services are not invoked.
            _kuCoinServiceMock.Verify(s => s.CancelOrder(It.IsAny<string>(), It.IsAny<KuCoinConfig>()), Times.Never);
            _kuCoinServiceMock.Verify(s => s.PlaceOrder(It.IsAny<OrderRequest>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
            _kuCoinServiceMock.Verify(s => s.GetOrderDetails(It.IsAny<string>(), It.IsAny<KuCoinConfig>()),
                Times.Never);
        }
    }
}