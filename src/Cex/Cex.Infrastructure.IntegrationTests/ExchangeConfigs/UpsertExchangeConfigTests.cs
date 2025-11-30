using Cex.Application.Common.Abstractions;
using Cex.Application.ExchangeConfigs.Commands.UpsertExchangeConfig;
using Cex.Application.ExchangeConfigs.Queries.GetExchangeConfigs;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.ExchangeConfigs
{
    public class UpsertExchangeConfigTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly ISender _sender;

        public UpsertExchangeConfigTests()
        {
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async Task Should_Create_New_ExchangeConfig_Successfully()
        {
            // Arrange
            var command = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "test-api-key-123",
                Secret: "test-secret-456",
                Passphrase: null
            );

            // Act
            var result = await _sender.Send(command);

            // Assert
            result.ShouldNotBeNull();
            result.ExchangeName.ShouldBe(ExchangeName.Binance);
            result.ApiKey.ShouldBe("test-api-key-123");
            result.Secret.ShouldBe("test-secret-456");
            result.Passphrase.ShouldBeNull();

            var entity = await _context.ExchangeConfigs
                .FirstOrDefaultAsync(x => x.UserId == _currentUser.Id && x.ExchangeName == ExchangeName.Binance);

            entity.ShouldNotBeNull();
            entity.UserId.ShouldBe(_currentUser.Id);
            entity.ExchangeName.ShouldBe(ExchangeName.Binance);
            entity.ApiKey.ShouldBe("test-api-key-123");
            entity.Secret.ShouldBe("test-secret-456");
            entity.Passphrase.ShouldBeNull();
        }

        [Fact]
        public async Task Should_Create_ExchangeConfig_With_Passphrase()
        {
            // Arrange
            var command = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.KuCoin,
                ApiKey: "kucoin-api-key",
                Secret: "kucoin-secret",
                Passphrase: "kucoin-passphrase"
            );

            // Act
            var result = await _sender.Send(command);

            // Assert
            result.ShouldNotBeNull();
            result.ExchangeName.ShouldBe(ExchangeName.KuCoin);
            result.ApiKey.ShouldBe("kucoin-api-key");
            result.Secret.ShouldBe("kucoin-secret");
            result.Passphrase.ShouldBe("kucoin-passphrase");

            var entity = await _context.ExchangeConfigs
                .FirstOrDefaultAsync(x => x.UserId == _currentUser.Id && x.ExchangeName == ExchangeName.KuCoin);

            entity.ShouldNotBeNull();
            entity.Passphrase.ShouldBe("kucoin-passphrase");
        }

        [Fact]
        public async Task Should_Update_Existing_ExchangeConfig()
        {
            // Arrange - Create initial config
            var createCommand = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Coinbase,
                ApiKey: "old-api-key",
                Secret: "old-secret",
                Passphrase: null
            );
            await _sender.Send(createCommand);

            // Act - Update the config
            var updateCommand = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Coinbase,
                ApiKey: "new-api-key",
                Secret: "new-secret",
                Passphrase: "new-passphrase"
            );
            var result = await _sender.Send(updateCommand);

            // Assert
            result.ShouldNotBeNull();
            result.ExchangeName.ShouldBe(ExchangeName.Coinbase);
            result.ApiKey.ShouldBe("new-api-key");
            result.Secret.ShouldBe("new-secret");
            result.Passphrase.ShouldBe("new-passphrase");

            var entity = await _context.ExchangeConfigs
                .FirstOrDefaultAsync(x => x.UserId == _currentUser.Id && x.ExchangeName == ExchangeName.Coinbase);

            entity.ShouldNotBeNull();
            entity.ApiKey.ShouldBe("new-api-key");
            entity.Secret.ShouldBe("new-secret");
            entity.Passphrase.ShouldBe("new-passphrase");

            // Verify only one record exists
            var count = await _context.ExchangeConfigs
                .CountAsync(x => x.UserId == _currentUser.Id && x.ExchangeName == ExchangeName.Coinbase);
            count.ShouldBe(1);
        }

        [Fact]
        public async Task Should_Update_And_Remove_Passphrase()
        {
            // Arrange - Create config with passphrase
            var createCommand = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Kraken,
                ApiKey: "api-key",
                Secret: "secret",
                Passphrase: "passphrase"
            );
            await _sender.Send(createCommand);

            // Act - Update without passphrase
            var updateCommand = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Kraken,
                ApiKey: "updated-api-key",
                Secret: "updated-secret",
                Passphrase: null
            );
            var result = await _sender.Send(updateCommand);

            // Assert
            result.ShouldNotBeNull();
            result.Passphrase.ShouldBeNull();

            var entity = await _context.ExchangeConfigs
                .FirstOrDefaultAsync(x => x.UserId == _currentUser.Id && x.ExchangeName == ExchangeName.Kraken);

            entity.ShouldNotBeNull();
            entity.Passphrase.ShouldBeNull();
        }

        [Fact]
        public async Task Should_Create_Multiple_Configs_For_Different_Exchanges()
        {
            // Arrange & Act
            var binanceCommand = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "binance-key",
                Secret: "binance-secret",
                Passphrase: null
            );
            var binanceResult = await _sender.Send(binanceCommand);

            var kucoinCommand = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.KuCoin,
                ApiKey: "kucoin-key",
                Secret: "kucoin-secret",
                Passphrase: "kucoin-pass"
            );
            var kucoinResult = await _sender.Send(kucoinCommand);

            // Assert
            binanceResult.ShouldNotBeNull();
            binanceResult.ExchangeName.ShouldBe(ExchangeName.Binance);

            kucoinResult.ShouldNotBeNull();
            kucoinResult.ExchangeName.ShouldBe(ExchangeName.KuCoin);

            var count = await _context.ExchangeConfigs
                .CountAsync(x => x.UserId == _currentUser.Id);
            count.ShouldBeGreaterThanOrEqualTo(2);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Should_Fail_When_ApiKey_Is_Empty(string apiKey)
        {
            // Arrange
            var command = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: apiKey,
                Secret: "secret",
                Passphrase: null
            );

            // Act & Assert
            await Should.ThrowAsync<Lib.Application.Exceptions.UnprocessableEntityException>(
                async () => await _sender.Send(command)
            );
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Should_Fail_When_Secret_Is_Empty(string secret)
        {
            // Arrange
            var command = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "api-key",
                Secret: secret,
                Passphrase: null
            );

            // Act & Assert
            await Should.ThrowAsync<Lib.Application.Exceptions.UnprocessableEntityException>(
                async () => await _sender.Send(command)
            );
        }

        [Fact]
        public async Task Should_Fail_When_KuCoin_Passphrase_Is_Missing()
        {
            // Arrange
            var command = new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.KuCoin,
                ApiKey: "api-key",
                Secret: "secret",
                Passphrase: null
            );

            // Act & Assert
            await Should.ThrowAsync<Lib.Application.Exceptions.UnprocessableEntityException>(
                async () => await _sender.Send(command)
            );
        }
    }
}
