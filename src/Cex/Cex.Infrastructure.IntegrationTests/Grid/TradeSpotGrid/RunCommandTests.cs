using System.Globalization;
using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.Commands.TradeSpotGrid;
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
    public class RunCommandTests : DependencyInjectionFixture
    {
        /// <summary>
        ///     Await buy step is matched the market price.
        ///     Expect:
        ///     - Grid quote balance DECREASED by BuyPrice * Qty
        ///     - Await buy step order id is not null
        ///     - Await buy step status changed to BuyOrderPlaced
        ///     - KuCoinService.PlaceOrder called once
        /// </summary>
        [Theory]
        [InlineData(60, 57, 58)]
        [InlineData(68, 71.4, 72.5)]
        public async Task Handle_AwaitingBuyStep_Successfully(decimal buyPrice, decimal lowestPrice,
            decimal highestPrice)
        {
            // Arrange
            var kuCoinServiceMock = new Mock<IKuCoinService>();
            kuCoinServiceMock.Setup(x => x.PlaceOrder(It.IsAny<OrderRequest>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_order_id");
            ServiceCollection.AddSingleton(kuCoinServiceMock.Object);

            var context = GetService<ICexDbContext>();
            var oGrid = context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            var quoteBalance = oGrid.QuoteBalance;

            // Act
            var sender = GetService<ISender>();
            await sender.Send(new RunCommand(oGrid,
                new Kline { LowestPrice = lowestPrice, HighestPrice = highestPrice }));

            // Assert: AwaitingBuy step should be updated.
            var grid = context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var step = context.SpotGridSteps
                .FirstOrDefault(step => step.BuyPrice == buyPrice &&
                                        step.SpotGridId == SpotGridCreated.Id &&
                                        step.Status == SpotGridStepStatus.BuyOrderPlaced);
            step.ShouldNotBeNull();
            step.BuyPrice.ShouldBe(buyPrice);
            step.OrderId.ShouldBe("fake_order_id");
            step.Status.ShouldBe(SpotGridStepStatus.BuyOrderPlaced);

            grid.QuoteBalance.ShouldBe((quoteBalance - step.Qty * step.BuyPrice).FixedNumber());

            kuCoinServiceMock.Verify(s => s.PlaceOrder(It.Is<OrderRequest>(req =>
                    req.Symbol == grid.Symbol &&
                    req.Side == "buy" &&
                    req.Type == "limit" &&
                    req.Price == step.BuyPrice.ToString(CultureInfo.InvariantCulture) &&
                    req.Size == step.Qty.ToString(CultureInfo.InvariantCulture))
                , It.IsAny<KuCoinConfig>()), Times.Once);
        }

        /// <summary>
        ///     Buy order was filled.
        ///     Expect:
        ///     - Grid base balance INCREASED by Qty
        ///     - BuyOrderPlaced step status changed to AwaitingSell
        ///     - AwaitingSell step order id is null
        ///     - AwaitingSell step order added
        ///     - KuCoinService.GetDetails called once
        /// </summary>
        [Theory]
        [InlineData(60, 57, 58)]
        [InlineData(68, 71.4, 72.5)]
        public async Task Handle_BuyOrderPlacedStep_Successfully(decimal buyPrice, decimal lowestPrice,
            decimal highestPrice)
        {
            // Arrange
            var kuCoinServiceMock = new Mock<IKuCoinService>();
            kuCoinServiceMock
                .Setup(x => x.GetOrderDetails(
                    It.Is<string>(orderId => orderId == "fake_order_id"), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "fake_order_id",
                    Type = "limit",
                    Side = "buy",
                    Price = buyPrice.ToString(CultureInfo.InvariantCulture),
                    IsActive = false,
                    Size = "0.5",
                    DealSize = "0.5",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });
            ServiceCollection.AddSingleton(kuCoinServiceMock.Object);

            var context = GetService<ICexDbContext>();
            var oStep = context.SpotGridSteps
                .First(x => x.SpotGridId == SpotGridCreated.Id
                            && x.BuyPrice == buyPrice
                            && x.Status == SpotGridStepStatus.AwaitingBuy);
            oStep.OrderId = "fake_order_id";
            oStep.Status = SpotGridStepStatus.BuyOrderPlaced;
            await context.SaveChangesAsync(default);

            // Act
            var oGrid = context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            await GetService<ISender>().Send(new RunCommand(oGrid,
                new Kline { LowestPrice = lowestPrice, HighestPrice = highestPrice }));

            // Assert
            var grid = context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var step = context.SpotGridSteps
                .Include(step => step.Orders)
                .FirstOrDefault(step => step.BuyPrice == buyPrice &&
                                        step.SpotGridId == SpotGridCreated.Id);
            step.ShouldNotBeNull();
            step.OrderId.ShouldBeNull();
            step.Status.ShouldBe(SpotGridStepStatus.AwaitingSell);
            step.Orders.ShouldHaveSingleItem();

            grid.BaseBalance.ShouldBe(SpotGridCreated.BaseBalance + step.Qty);

            var order = step.Orders.FirstOrDefault();
            order.ShouldNotBeNull();
            order.OrderId.ShouldBe("fake_order_id");
            order.Symbol.ShouldBe(SpotGridCreated.Symbol);
            order.Price.ShouldBe(buyPrice);
            order.OrigQty.ShouldBe(0.5m);
            order.Type.ShouldBe("limit");
            order.Side.ShouldBe("buy");
            order.Fee.ShouldBe(1);
            order.FeeCurrency.ShouldBe("USDT");

            kuCoinServiceMock.Verify(
                s => s.GetOrderDetails(
                    It.Is<string>(orderId => orderId == "fake_order_id"), It.IsAny<KuCoinConfig>())
                , Times.Once);
        }

        /// <summary>
        ///     AwaitingSell step is matched the market price.
        ///     Expect:
        ///     - Grid base balance DECREASED by Qty
        ///     - Await buy step order id is not null
        ///     - Await buy step status changed to SellOrderPlaced
        ///     - KuCoinService.PlaceOrder called once
        /// </summary>
        [Theory]
        [InlineData(62, 58, 58.9)]
        [InlineData(70, 71.4, 72.5)]
        public async Task Handle_AwaitingSell_Successfully(decimal sellPrice, decimal lowestPrice,
            decimal highestPrice)
        {
            // Arrange
            var kuCoinServiceMock = new Mock<IKuCoinService>();
            kuCoinServiceMock.Setup(x => x.PlaceOrder(It.IsAny<OrderRequest>(), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync("fake_order_id");
            ServiceCollection.AddSingleton(kuCoinServiceMock.Object);

            var context = GetService<ICexDbContext>();
            var oGrid = context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);

            // 1. Arrange: AwaitingSell step
            var oStep = oGrid.GridSteps
                .First(x => x.SpotGridId == SpotGridCreated.Id && x.SellPrice == sellPrice);
            oStep.OrderId = null;
            oStep.Status = SpotGridStepStatus.AwaitingSell;

            // 2. Arrange: Grid base balance
            const int baseBalance = 100;
            oGrid.BaseBalance = baseBalance;
            await context.SaveChangesAsync(default);

            // Act
            var sender = GetService<ISender>();
            await sender.Send(new RunCommand(oGrid,
                new Kline { LowestPrice = lowestPrice, HighestPrice = highestPrice }));

            // Assert: AwaitingBuy step should be updated.
            var grid = context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var step = context.SpotGridSteps
                .FirstOrDefault(step => step.SellPrice == sellPrice && step.SpotGridId == SpotGridCreated.Id);

            // 1. Verify AwaitingSell step is updated.
            step.ShouldNotBeNull();
            step.SellPrice.ShouldBe(sellPrice);
            step.OrderId.ShouldBe("fake_order_id");
            step.Status.ShouldBe(SpotGridStepStatus.SellOrderPlaced);

            // 2. Verify Grid base balance is decreased by Qty.
            grid.BaseBalance.ShouldBe(baseBalance - step.Qty);

            kuCoinServiceMock.Verify(s => s.PlaceOrder(It.Is<OrderRequest>(req =>
                    req.Symbol == grid.Symbol &&
                    req.Side == "sell" &&
                    req.Type == "limit" &&
                    req.Price == step.SellPrice.ToString(CultureInfo.InvariantCulture) &&
                    req.Size == step.Qty.ToString(CultureInfo.InvariantCulture))
                , It.IsAny<KuCoinConfig>()), Times.Once);
        }

        /// <summary>
        ///     SellOrderPlaced was filled.
        ///     Expect:
        ///     - Grid quote balance INCREASED by Qty * SellPrice
        ///     - SellOrderPlaced step status changed to AwaitingBuy
        ///     - AwaitingBuy step order id is null
        ///     - AwaitingBuy step order added
        ///     - KuCoinService.GetDetails called once
        /// </summary>
        [Theory]
        [InlineData(62, 58, 58.9)]
        [InlineData(70, 71.4, 72.5)]
        public async Task Handle_SellOrderPlacedStep_Successfully(decimal sellPrice, decimal lowestPrice,
            decimal highestPrice)
        {
            // Arrange
            var kuCoinServiceMock = new Mock<IKuCoinService>();
            kuCoinServiceMock
                .Setup(x => x.GetOrderDetails(
                    It.Is<string>(orderId => orderId == "fake_order_id"), It.IsAny<KuCoinConfig>()))
                .ReturnsAsync(new OrderDetails
                {
                    Id = "fake_order_id",
                    Type = "limit",
                    Side = "sell",
                    Price = sellPrice.ToString(CultureInfo.InvariantCulture),
                    IsActive = false,
                    Size = "0.5",
                    DealSize = "0.5",
                    Fee = "1",
                    FeeCurrency = "USDT",
                    CreatedAt = DateTime.UtcNow.ToUnixTimestampMilliseconds()
                });
            ServiceCollection.AddSingleton(kuCoinServiceMock.Object);

            var context = GetService<ICexDbContext>();
            await context.SpotGridSteps
                .Where(x => x.SpotGridId == SpotGridCreated.Id).ForEachAsync(step =>
                {
                    step.Status = SpotGridStepStatus.SellOrderPlaced;
                    if (step.SellPrice == sellPrice)
                    {
                        step.OrderId = "fake_order_id";
                    }
                });
            await context.SaveChangesAsync(default);

            // Act
            var oGrid = context.SpotGrids
                .Include(x => x.GridSteps)
                .First(x => x.Id == SpotGridCreated.Id);
            await GetService<ISender>().Send(new RunCommand(oGrid,
                new Kline { LowestPrice = lowestPrice, HighestPrice = highestPrice }));

            // Assert
            var grid = context.SpotGrids.First(x => x.Id == SpotGridCreated.Id);
            var step = context.SpotGridSteps
                .Include(step => step.Orders)
                .FirstOrDefault(step => step.SellPrice == sellPrice && step.SpotGridId == SpotGridCreated.Id);

            // 1. Verify SellOrderPlaced step is updated.
            step.ShouldNotBeNull();
            step.OrderId.ShouldBeNull();
            step.Status.ShouldBe(SpotGridStepStatus.AwaitingBuy);
            step.Orders.ShouldHaveSingleItem();

            // 2. Verify Grid quote balance is increased by Qty * SellPrice.
            grid.QuoteBalance.ShouldBe((SpotGridCreated.QuoteBalance + step.Qty * step.SellPrice).FixedNumber());

            // 3. Verify order details are added.
            var order = step.Orders.FirstOrDefault();
            order.ShouldNotBeNull();
            order.OrderId.ShouldBe("fake_order_id");
            order.Symbol.ShouldBe(SpotGridCreated.Symbol);
            order.Price.ShouldBe(sellPrice);
            order.OrigQty.ShouldBe(0.5m);
            order.Type.ShouldBe("limit");
            order.Side.ShouldBe("sell");
            order.Fee.ShouldBe(1);
            order.FeeCurrency.ShouldBe("USDT");

            // 4. Verify KuCoinService.GetDetails is called.
            kuCoinServiceMock.Verify(
                s => s.GetOrderDetails(
                    It.Is<string>(orderId => orderId == "fake_order_id"), It.IsAny<KuCoinConfig>())
                , Times.Once);
        }
    }
}