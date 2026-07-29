using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Extensions;
using Lib.Application.Logging;
using Lib.ExternalServices.Bnb;
using Lib.ExternalServices.Bnb.Models;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public class SyncSpotOrders(
        ILogTrace logTrace,
        IBnbService bndService,
        ICexDbContext dbContext)
    {
        protected readonly IBnbService _bndService = bndService;
        protected readonly ILogTrace _logTrace = logTrace;
        protected readonly ICexDbContext DbContext = dbContext;

        protected async Task<SpotOrderSyncSettingDto> Sync(Domain.Entities.BnbSetting setting
            , SpotOrderSyncSetting syncSetting, long serverTime, CancellationToken cancellationToken)
        {
            var spotOrders = await _bndService.AllOrders(setting.ApiKey,
                new AllOrdersRequest(syncSetting.Symbol, serverTime,
                    syncSetting.LastSyncAt.ToUnixTimestampMilliseconds(), setting.SecretKey));
            if (spotOrders.Count == 0)
            {
                return await UpdateLastSyncToSyncSetting(syncSetting, cancellationToken);
            }

            var spotOrderEntities = spotOrders.Select(raw => new SpotOrder
            {
                UserId = setting.UserId,
                Symbol = raw.Symbol,
                OrderId = raw.OrderId.ToString(),
                ClientOrderId = raw.ClientOrderId,
                Price = decimal.Parse(raw.Price ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                OrigQty = decimal.Parse(raw.OrigQty ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                TimeInForce = raw.TimeInForce,
                Type = raw.Type,
                Side = raw.Side,
                IsWorking = raw.IsWorking,
                CreatedAt = raw.Time.ToDateTimeFromMilliseconds(),
                UpdatedAt = raw.UpdateTime.ToDateTimeFromMilliseconds(),
                WorkingTime = raw.WorkingTime.ToDateTimeFromMilliseconds()
            }).ToList();

            // insert spot orders
            await DbContext.SpotOrders.AddRangeAsync(spotOrderEntities, cancellationToken);

            // update sync setting
            var lastSyncAt = spotOrders.Max(x => x.UpdateTime);
            var ss = await DbContext.SpotOrderSyncSettings.FirstAsync(
                x => x.UserId == setting.UserId && x.Symbol == syncSetting.Symbol, cancellationToken);
            ss.LastSyncAt = lastSyncAt.ToDateTimeFromMilliseconds();
            DbContext.SpotOrderSyncSettings.Update(ss);
            _logTrace.LogInformation($"Last Sync {syncSetting.Symbol} at {ss.LastSyncAt}");
            await DbContext.SaveChangesAsync(cancellationToken);
            return SpotOrderSyncSettingDto.From(ss);
        }

        private async Task<SpotOrderSyncSettingDto> UpdateLastSyncToSyncSetting(SpotOrderSyncSetting syncSetting,
            CancellationToken cancellationToken)
        {
            var ss = await DbContext.SpotOrderSyncSettings.FirstAsync(
                x => x.UserId == syncSetting.UserId && x.Symbol == syncSetting.Symbol, cancellationToken);
            var spotOrdersQuery =
                DbContext.SpotOrders.Where(x =>
                    x.UserId == syncSetting.UserId && x.Symbol == syncSetting.Symbol);

            if (await spotOrdersQuery.AnyAsync(cancellationToken))
            {
                var lastSyncAt = spotOrdersQuery.Max(x => x.UpdatedAt);
                ss.LastSyncAt = lastSyncAt;
                DbContext.SpotOrderSyncSettings.Update(ss);
                _logTrace.LogInformation($"Update Last Sync {syncSetting.Symbol} at {ss.LastSyncAt}");
                await DbContext.SaveChangesAsync(cancellationToken);
            }

            return SpotOrderSyncSettingDto.From(ss);
        }
    }
}
