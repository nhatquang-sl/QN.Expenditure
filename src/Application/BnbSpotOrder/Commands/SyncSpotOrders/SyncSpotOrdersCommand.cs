using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using Application.Common.ExServices.Bnb;
using Application.Common.ExServices.Bnb.Models;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public class SyncSpotOrdersCommand : IRequest
    {
    }

    public class SyncSpotOrdersCommandHandler : IRequestHandler<SyncSpotOrdersCommand>
    {
        private readonly IMapper _mapper;
        private readonly IBndService _bndService;
        private readonly IApplicationDbContext _applicationDbContext;

        public SyncSpotOrdersCommandHandler(IMapper mapper, IApplicationDbContext applicationDbContext, IBndService bndService)
        {
            _mapper = mapper;
            _bndService = bndService;
            _applicationDbContext = applicationDbContext;
        }

        public async Task Handle(SyncSpotOrdersCommand request, CancellationToken cancellationToken)
        {
            var settings = await _applicationDbContext.BnbSettings
                .Where(x => x.UserId != null && x.ApiKey != null && x.SecretKey != null)
                .ToListAsync(cancellationToken);

            await Task.WhenAll(settings.Select(x => SyncSpotOrdersByUser(x, cancellationToken)));

            await _applicationDbContext.SaveChangesAsync(cancellationToken);
        }

        async Task SyncSpotOrdersByUser(Domain.Entities.BnbSetting setting, CancellationToken cancellationToken)
        {
            var syncSettings = await _applicationDbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == setting.UserId)
                .ToListAsync(cancellationToken);

            var serverTimeRes = await _bndService.GetServerTime();

            await Task.WhenAll(syncSettings.Select(x => SyncSpotOrders(setting, _mapper.Map<SpotOrderSyncSettingDto>(x), serverTimeRes.ServerTime, cancellationToken)));
        }

        async Task SyncSpotOrders(Domain.Entities.BnbSetting setting, SpotOrderSyncSettingDto syncSetting, long serverTime, CancellationToken cancellationToken)
        {
            var spotOrders = await _bndService.AllOrders(setting.ApiKey, new AllOrdersRequest(syncSetting.Symbol, serverTime, syncSetting.LastSyncAt, setting.SecretKey));
            if (spotOrders.Count == 0) return;

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
        }
    }
}
