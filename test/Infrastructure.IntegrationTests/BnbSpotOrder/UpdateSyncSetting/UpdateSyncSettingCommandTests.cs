using Application.BnbSpotOrder.Commands.UpdateSyncSetting;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Extensions;
using Domain.Entities;
using MediatR;
using Shouldly;

namespace Infrastructure.IntegrationTests.BnbSpotOrder.UpdateSyncSetting
{
    public class UpdateSyncSettingCommandTests : DependencyInjectionFixture
    {
        private readonly ISender _sender;
        private readonly ICurrentUser _currentUser;
        private readonly IApplicationDbContext _context;

        public UpdateSyncSettingCommandTests() : base()
        {
            _sender = GetService<ISender>();
            _context = GetService<IApplicationDbContext>();
            _currentUser = GetService<ICurrentUser>();
        }

        [Fact]
        public async void Failed_Symbol_NotFound()
        {
            // Arrange
            var lastSyncAt = DateTime.UtcNow.ToUnixTimestampMilliseconds();

            // Act
            var exception = await Should.ThrowAsync<NotFoundException>(()
                => _sender.Send(new UpdateSyncSettingCommand("IDUSDT", lastSyncAt)));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""IDUSDT not found.""}");
        }

        [Fact]
        public async void Success_Without_Spot_Orders()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var updatedAt = DateTime.UtcNow.AddHours(1).ToUnixTimestampMilliseconds().ToDateTimeFromMilliseconds();
            _context.SpotOrderSyncSettings.Add(new SpotOrderSyncSetting
            {
                UserId = _currentUser.Id,
                Symbol = "IDUSDT",
                LastSyncAt = createdAt
            });
            await _context.SaveChangesAsync(new CancellationToken());

            // Act
            var res = await _sender.Send(new UpdateSyncSettingCommand("IDUSDT", updatedAt.ToUnixTimestampMilliseconds()));

            // Assert
            res.ShouldNotBeNull();
            res.Symbol.ShouldBe("IDUSDT");
            res.LastSyncAt.ShouldBe(updatedAt.ToUnixTimestampMilliseconds());
            var entity = _context.SpotOrderSyncSettings.Where(x => x.UserId == _currentUser.Id && x.Symbol == "IDUSDT").FirstOrDefault();
            entity.ShouldNotBeNull();
            entity.Symbol.ShouldBe("IDUSDT");
            entity.LastSyncAt.ShouldBe(updatedAt);
        }

        [Fact]
        public async void Success_With_Spot_Orders()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var updatedAt = DateTime.UtcNow.AddHours(1).ToUnixTimestampMilliseconds().ToDateTimeFromMilliseconds();
            _context.SpotOrders.Add(SpotOrderData.Generate(_currentUser.Id, "IDUSDT", createdAt));
            _context.SpotOrderSyncSettings.Add(new SpotOrderSyncSetting
            {
                UserId = _currentUser.Id,
                Symbol = "IDUSDT",
                LastSyncAt = createdAt
            });
            await _context.SaveChangesAsync(new CancellationToken());

            // Act
            var res = await _sender.Send(new UpdateSyncSettingCommand("IDUSDT", updatedAt.ToUnixTimestampMilliseconds()));

            // Assert
            res.ShouldNotBeNull();
            res.Symbol.ShouldBe("IDUSDT");
            res.LastSyncAt.ShouldBe(updatedAt.ToUnixTimestampMilliseconds());
            var entity = _context.SpotOrderSyncSettings.Where(x => x.UserId == _currentUser.Id && x.Symbol == "IDUSDT").FirstOrDefault();
            entity.ShouldNotBeNull();
            entity.Symbol.ShouldBe("IDUSDT");
            entity.LastSyncAt.ShouldBe(updatedAt);
        }

        [Fact]
        public async void Failed_LastSyncAt_Less_Than_Spot_Orders()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var updatedAt = DateTime.UtcNow.AddHours(-1).ToUnixTimestampMilliseconds().ToDateTimeFromMilliseconds();
            _context.SpotOrders.Add(SpotOrderData.Generate(_currentUser.Id, "IDUSDT", createdAt));
            _context.SpotOrderSyncSettings.Add(new SpotOrderSyncSetting
            {
                UserId = _currentUser.Id,
                Symbol = "IDUSDT",
                LastSyncAt = createdAt
            });
            await _context.SaveChangesAsync(new CancellationToken());

            // Act
            var exception = await Should.ThrowAsync<BadRequestException>(()
                => _sender.Send(new UpdateSyncSettingCommand("IDUSDT", updatedAt.ToUnixTimestampMilliseconds())));

            // Assert
            exception.Message.ShouldBe(@"{""message"":""Last Sync At is greater than last Spot Order sync.""}");
        }
    }
}
