using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.ExServices.Bnb;
using Application.Common.Logging;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.SyncSpotOrders
{
    public record SyncSpotOrdersBySymbolCommand(string Symbol) : IRequest<SpotOrderSyncSettingDto> { }

    public class SyncSpotOrdersBySymbolCommandHandler(IMapper mapper, ILogTrace logTrace
            , ICurrentUser currentUser, IApplicationDbContext applicationDbContext, IBndService bndService) : SyncSpotOrders(mapper, logTrace, bndService, applicationDbContext)
        , IRequestHandler<SyncSpotOrdersBySymbolCommand, SpotOrderSyncSettingDto>
    {
        private readonly ICurrentUser _currentUser = currentUser;

        public async Task<SpotOrderSyncSettingDto> Handle(SyncSpotOrdersBySymbolCommand request, CancellationToken cancellationToken)
        {
            var setting = await _applicationDbContext.BnbSettings
                .Where(x => x.UserId == _currentUser.Id && x.ApiKey != null && x.SecretKey != null)
                .FirstOrDefaultAsync(cancellationToken);

            var syncSetting = await _applicationDbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == _currentUser.Id && x.Symbol == request.Symbol)
                .FirstOrDefaultAsync(cancellationToken);

            if (setting == null || syncSetting == null) throw new NotFoundException("");

            var serverTimeRes = await _bndService.GetServerTime();
            return await Sync(setting, syncSetting, serverTimeRes.ServerTime, cancellationToken);
        }
    }
}
