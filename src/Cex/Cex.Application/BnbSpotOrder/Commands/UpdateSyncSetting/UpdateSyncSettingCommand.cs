using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using Lib.Application.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Commands.UpdateSyncSetting
{
    public record UpdateSyncSettingCommand(string Symbol, long LastSyncAt) : IRequest<SpotOrderSyncSettingDto>;

    public class UpdateSyncSettingCommandHandler(ICurrentUser currentUser, ICexDbContext dbContext)
        : IRequestHandler<UpdateSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        public async Task<SpotOrderSyncSettingDto> Handle(UpdateSyncSettingCommand request,
            CancellationToken cancellationToken)
        {
            var entity =
                await dbContext.SpotOrderSyncSettings.FirstOrDefaultAsync(
                    x => x.Symbol == request.Symbol && x.UserId == currentUser.Id, cancellationToken)
                ?? throw new NotFoundException($"{request.Symbol} is not found.");

            entity.LastSyncAt = request.LastSyncAt.ToDateTimeFromMilliseconds();

            dbContext.SpotOrderSyncSettings.Update(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            return SpotOrderSyncSettingDto.From(entity);
        }
    }
}
