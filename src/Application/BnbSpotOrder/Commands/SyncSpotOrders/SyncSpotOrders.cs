using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using Application.Common.ExServices.Bnb;
using Application.Common.ExServices.Bnb.Models;
using Application.Common.Extensions;
using Application.Common.Logging;
using AutoMapper;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public class SyncSpotOrders(IMapper mapper, ILogTrace logTrace, IBndService bndService, IApplicationDbContext applicationDbContext)
    {
        protected readonly IMapper _mapper = mapper;
        protected readonly ILogTrace _logTrace = logTrace;
        protected readonly IBndService _bndService = bndService;
        protected readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        protected async Task<SpotOrderSyncSettingDto> Sync(Domain.Entities.BnbSetting setting
        , SpotOrderSyncSetting syncSetting, long serverTime, CancellationToken cancellationToken)
        {
            var spotOrders = await _bndService.AllOrders(setting.ApiKey, new AllOrdersRequest(syncSetting.Symbol, serverTime, syncSetting.LastSyncAt.ToUnixTimestampMilliseconds(), setting.SecretKey));
            if (spotOrders.Count == 0)
            {
                return await UpdateLastSyncToSyncSetting(syncSetting, cancellationToken);
            };

            var spotOrderEntities = _mapper.Map<List<SpotOrder>>(spotOrders)?
                .Select(x => { x.UserId = setting.UserId; return x; })
                .ToList();

            // insert spot orders
            await _applicationDbContext.SpotOrders.AddRangeAsync(spotOrderEntities ?? [], cancellationToken);

            // update sync setting
            var lastSyncAt = spotOrders.Max(x => x.UpdateTime);
            var ss = await _applicationDbContext.SpotOrderSyncSettings.FirstAsync(x => x.UserId == setting.UserId && x.Symbol == syncSetting.Symbol, cancellationToken);
            ss.LastSyncAt = lastSyncAt.ToDateTimeFromMilliseconds();
            _applicationDbContext.SpotOrderSyncSettings.Update(ss);
            _logTrace.LogInformation($"Last Sync {syncSetting.Symbol} at {ss.LastSyncAt}");
            await _applicationDbContext.SaveChangesAsync(cancellationToken);
            return _mapper.Map<SpotOrderSyncSettingDto>(ss);
        }

        async Task<SpotOrderSyncSettingDto> UpdateLastSyncToSyncSetting(SpotOrderSyncSetting syncSetting, CancellationToken cancellationToken)
        {
            var ss = await _applicationDbContext.SpotOrderSyncSettings.FirstAsync(x => x.UserId == syncSetting.UserId && x.Symbol == syncSetting.Symbol, cancellationToken);
            var spotOrdersQuery = _applicationDbContext.SpotOrders.Where(x => x.UserId == syncSetting.UserId && x.Symbol == syncSetting.Symbol);

            if (await spotOrdersQuery.AnyAsync(cancellationToken))
            {
                var lastSyncAt = spotOrdersQuery.Max(x => x.UpdateTime);
                ss.LastSyncAt = lastSyncAt;
                _applicationDbContext.SpotOrderSyncSettings.Update(ss);
                _logTrace.LogInformation($"Update Last Sync {syncSetting.Symbol} at {ss.LastSyncAt}");
                await _applicationDbContext.SaveChangesAsync(cancellationToken);
            }

            return _mapper.Map<SpotOrderSyncSettingDto>(ss);
        }
    }
}
