using Application.Common.Abstractions;
using Application.Common.Logging;
using AutoMapper;
using Lib.ExternalServices.Bnb;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Refit;

namespace Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public class SyncAllSpotOrdersCommand : IRequest { }

    public class SyncSpotOrdersCommandHandler(IMapper mapper, ILogTrace logTrace
            , IApplicationDbContext applicationDbContext, IBnbService bndService)
        : SyncSpotOrders(mapper, logTrace, bndService, applicationDbContext)
        , IRequestHandler<SyncAllSpotOrdersCommand>
    {
        public async Task Handle(SyncAllSpotOrdersCommand request, CancellationToken cancellationToken)
        {
            var settings = await _applicationDbContext.BnbSettings
                .Where(x => x.UserId != null && x.ApiKey != null && x.SecretKey != null)
                .ToListAsync(cancellationToken);

            foreach (var setting in settings)
            {
                await SyncSpotOrdersByUser(setting, cancellationToken);
            }
        }

        async Task SyncSpotOrdersByUser(Domain.Entities.BnbSetting setting, CancellationToken cancellationToken)
        {
            var syncSettings = await _applicationDbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == setting.UserId)
                .ToListAsync(cancellationToken);

            try
            {
                var serverTimeRes = await _bndService.GetServerTime();
                foreach (var syncSetting in syncSettings)
                {
                    await Sync(setting, syncSetting, serverTimeRes.ServerTime, cancellationToken);
                }
            }
            catch (ApiException ex)
            {
                _logTrace.LogError($"{ex.Message} - {ex.Content}");

                throw;
            }
        }
    }
}
