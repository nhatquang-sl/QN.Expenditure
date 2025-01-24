using AutoMapper;
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
        IMapper mapper,
        ILogTrace logTrace,
        IBnbService bndService,
        ICexDbContext dbContext)
    {
        protected readonly IBnbService _bndService = bndService;
        protected readonly ILogTrace _logTrace = logTrace;
        protected readonly IMapper _mapper = mapper;
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

            ;

            var spotOrderEntities = _mapper.Map<List<SpotOrder>>(spotOrders)?
                .Select(x =>
                {
                    x.UserId = setting.UserId;
                    return x;
                })
                .ToList();

            // insert spot orders
            await DbContext.SpotOrders.AddRangeAsync(spotOrderEntities ?? [], cancellationToken);

            // update sync setting
            var lastSyncAt = spotOrders.Max(x => x.UpdateTime);
            var ss = await DbContext.SpotOrderSyncSettings.FirstAsync(
                x => x.UserId == setting.UserId && x.Symbol == syncSetting.Symbol, cancellationToken);
            ss.LastSyncAt = lastSyncAt.ToDateTimeFromMilliseconds();
            DbContext.SpotOrderSyncSettings.Update(ss);
            _logTrace.LogInformation($"Last Sync {syncSetting.Symbol} at {ss.LastSyncAt}");
            await DbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SpotOrderSyncSettingDto>(ss);
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

            return _mapper.Map<SpotOrderSyncSettingDto>(ss);
        }
    }
}