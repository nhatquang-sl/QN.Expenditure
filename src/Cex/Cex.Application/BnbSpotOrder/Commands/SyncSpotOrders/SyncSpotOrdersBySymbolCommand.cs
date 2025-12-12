using AutoMapper;
using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using Lib.Application.Logging;
using Lib.ExternalServices.Bnb;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public record SyncSpotOrdersBySymbolCommand(string Symbol) : IRequest<SpotOrderSyncSettingDto>
    {
    }

    public class SyncSpotOrdersBySymbolCommandHandler(
        IMapper mapper,
        ILogTrace logTrace,
        ICurrentUser currentUser,
        ICexDbContext dbContext,
        IBnbService bndService)
        : Cex.Application.BnbSpotOrder.Commands.SyncSpotOrders.SyncSpotOrders(mapper, logTrace, bndService,
                dbContext)
            , IRequestHandler<SyncSpotOrdersBySymbolCommand, SpotOrderSyncSettingDto>
    {
        private readonly ICurrentUser _currentUser = currentUser;

        public async Task<SpotOrderSyncSettingDto> Handle(SyncSpotOrdersBySymbolCommand request,
            CancellationToken cancellationToken)
        {
            var setting = await DbContext.BnbSettings
                .Where(x => x.UserId == _currentUser.Id && x.ApiKey != null && x.SecretKey != null)
                .FirstOrDefaultAsync(cancellationToken);

            var syncSetting = await DbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == _currentUser.Id && x.Symbol == request.Symbol)
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null || syncSetting == null)
            {
                throw new NotFoundException("");
            }

            var serverTimeRes = await _bndService.GetServerTime();
            return await Sync(setting, syncSetting, serverTimeRes.ServerTime, cancellationToken);
        }
    }
}