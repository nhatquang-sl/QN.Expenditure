using AutoMapper;
using Cex.Application.Common.Abstractions;
using Lib.Application.Logging;
using Lib.ExternalServices.Bnb;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Refit;

namespace Cex.Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public class SyncAllSpotOrdersCommand : IRequest
    {
    }

    public class SyncSpotOrdersCommandHandler(
        IMapper mapper,
        ILogTrace logTrace,
        ICexDbContext dbContext,
        IBnbService bndService)
        : SyncSpotOrders(mapper, logTrace, bndService, dbContext)
            , IRequestHandler<SyncAllSpotOrdersCommand>
    {
        public async Task Handle(SyncAllSpotOrdersCommand request, CancellationToken cancellationToken)
        {
            var settings = await DbContext.BnbSettings
                .Where(x => x.UserId != null && x.ApiKey != null && x.SecretKey != null)
                .ToListAsync(cancellationToken);

            foreach (var setting in settings)
            {
                await SyncSpotOrdersByUser(setting, cancellationToken);
            }
        }

        private async Task SyncSpotOrdersByUser(Domain.Entities.BnbSetting setting, CancellationToken cancellationToken)
        {
            var syncSettings = await DbContext.SpotOrderSyncSettings
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