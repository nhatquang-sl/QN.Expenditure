using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using MediatR;

namespace Cex.Application.BnbSpotOrder.Commands.CreateSyncSetting
{
    public record CreateSyncSettingCommand(string Symbol, long LastSyncAt) : IRequest<SpotOrderSyncSettingDto>;

    public class CreateSyncSettingCommandHandler(ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<CreateSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        public async Task<SpotOrderSyncSettingDto> Handle(CreateSyncSettingCommand request,
            CancellationToken cancellationToken)
        {
            var entity = new SpotOrderSyncSetting
            {
                UserId = currentUser.Id,
                Symbol = request.Symbol,
                LastSyncAt = request.LastSyncAt.ToDateTimeFromMilliseconds()
            };

            cexDbContext.SpotOrderSyncSettings.Add(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return SpotOrderSyncSettingDto.From(entity);
        }
    }
}
