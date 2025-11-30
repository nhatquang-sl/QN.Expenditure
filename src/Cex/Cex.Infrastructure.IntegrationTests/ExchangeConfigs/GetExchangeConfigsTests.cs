using Cex.Application.Common.Abstractions;
using Cex.Application.ExchangeConfigs.Commands.UpsertExchangeConfig;
using Cex.Application.ExchangeConfigs.Queries.GetExchangeConfigs;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using MediatR;
using Shouldly;

namespace Cex.Infrastructure.IntegrationTests.ExchangeConfigs
{
    public class GetExchangeConfigsTests : DependencyInjectionFixture
    {
        private readonly ICexDbContext _context;
        private readonly ICurrentUser _currentUser;
        private readonly ISender _sender;

        public GetExchangeConfigsTests()
        {
            _sender = GetService<ISender>();
            _context = GetService<ICexDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async Task Should_Return_Empty_List_When_No_Configs_Exist()
        {
            // Arrange
            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeEmpty();
        }

        [Fact]
        public async Task Should_Return_All_Configs_For_Current_User()
        {
            // Arrange - Create multiple configs
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "binance-key",
                Secret: "binance-secret",
                Passphrase: null
            ));

            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.KuCoin,
                ApiKey: "kucoin-key",
                Secret: "kucoin-secret",
                Passphrase: "kucoin-pass"
            ));

            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Coinbase,
                ApiKey: "coinbase-key",
                Secret: "coinbase-secret",
                Passphrase: null
            ));

            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            result.ShouldNotBeNull();
            result.Count.ShouldBeGreaterThanOrEqualTo(3);
            result.ShouldContain(x => x.ExchangeName == ExchangeName.Binance);
            result.ShouldContain(x => x.ExchangeName == ExchangeName.KuCoin);
            result.ShouldContain(x => x.ExchangeName == ExchangeName.Coinbase);
        }

        [Fact]
        public async Task Should_Return_Configs_Ordered_By_ExchangeName()
        {
            // Arrange - Create configs in random order
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.KuCoin,
                ApiKey: "key1",
                Secret: "secret1",
                Passphrase: "pass1"
            ));

            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "key2",
                Secret: "secret2",
                Passphrase: null
            ));

            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Coinbase,
                ApiKey: "key3",
                Secret: "secret3",
                Passphrase: null
            ));

            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            result.ShouldNotBeNull();
            var exchanges = result.Where(x =>
                x.ExchangeName == ExchangeName.Binance ||
                x.ExchangeName == ExchangeName.Coinbase ||
                x.ExchangeName == ExchangeName.KuCoin
            ).ToList();

            exchanges.Count.ShouldBe(3);
            exchanges[0].ExchangeName.ShouldBe(ExchangeName.Binance);
            exchanges[1].ExchangeName.ShouldBe(ExchangeName.Coinbase);
            exchanges[2].ExchangeName.ShouldBe(ExchangeName.KuCoin);
        }

        [Fact]
        public async Task Should_Return_Config_With_All_Properties()
        {
            // Arrange
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Bybit,
                ApiKey: "test-api-key-12345",
                Secret: "test-secret-67890",
                Passphrase: "test-passphrase"
            ));

            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            var bybitConfig = result.FirstOrDefault(x => x.ExchangeName == ExchangeName.Bybit);
            bybitConfig.ShouldNotBeNull();
            bybitConfig.ExchangeName.ShouldBe(ExchangeName.Bybit);
            bybitConfig.ApiKey.ShouldBe("test-api-key-12345");
            bybitConfig.Secret.ShouldBe("test-secret-67890");
            bybitConfig.Passphrase.ShouldBe("test-passphrase");
        }

        [Fact]
        public async Task Should_Return_Config_Without_Passphrase()
        {
            // Arrange
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Kraken,
                ApiKey: "kraken-key",
                Secret: "kraken-secret",
                Passphrase: null
            ));

            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            var krakenConfig = result.FirstOrDefault(x => x.ExchangeName == ExchangeName.Kraken);
            krakenConfig.ShouldNotBeNull();
            krakenConfig.ExchangeName.ShouldBe(ExchangeName.Kraken);
            krakenConfig.ApiKey.ShouldBe("kraken-key");
            krakenConfig.Secret.ShouldBe("kraken-secret");
            krakenConfig.Passphrase.ShouldBeNull();
        }

        [Fact]
        public async Task Should_Return_Updated_Config_After_Upsert()
        {
            // Arrange - Create initial config
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "old-key",
                Secret: "old-secret",
                Passphrase: null
            ));

            // Update the config
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "new-key",
                Secret: "new-secret",
                Passphrase: "new-pass"
            ));

            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            var binanceConfig = result.FirstOrDefault(x => x.ExchangeName == ExchangeName.Binance);
            binanceConfig.ShouldNotBeNull();
            binanceConfig.ApiKey.ShouldBe("new-key");
            binanceConfig.Secret.ShouldBe("new-secret");
            binanceConfig.Passphrase.ShouldBe("new-pass");

            // Verify only one Binance config exists
            var binanceConfigs = result.Where(x => x.ExchangeName == ExchangeName.Binance).ToList();
            binanceConfigs.Count.ShouldBe(1);
        }

        [Fact]
        public async Task Should_Return_Multiple_Configs_With_Mixed_Passphrases()
        {
            // Arrange - Create configs with and without passphrases
            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.Binance,
                ApiKey: "binance-key",
                Secret: "binance-secret",
                Passphrase: null
            ));

            await _sender.Send(new UpsertExchangeConfigCommand(
                ExchangeName: ExchangeName.KuCoin,
                ApiKey: "kucoin-key",
                Secret: "kucoin-secret",
                Passphrase: "kucoin-passphrase"
            ));

            var query = new GetExchangeConfigsQuery();

            // Act
            var result = await _sender.Send(query);

            // Assert
            result.ShouldNotBeNull();

            var binance = result.FirstOrDefault(x => x.ExchangeName == ExchangeName.Binance);
            binance.ShouldNotBeNull();
            binance.Passphrase.ShouldBeNull();

            var kucoin = result.FirstOrDefault(x => x.ExchangeName == ExchangeName.KuCoin);
            kucoin.ShouldNotBeNull();
            kucoin.Passphrase.ShouldBe("kucoin-passphrase");
        }
    }
}
