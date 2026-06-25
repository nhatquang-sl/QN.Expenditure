using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Commands.DeleteSyncSetting
{
    public record DeleteSyncSettingCommand(string Symbol) : IRequest<SpotOrderSyncSettingDto>;

    public class DeleteSyncSettingCommandHandler(ICurrentUser currentUser, ICexDbContext applicationDbContext)
        : IRequestHandler<DeleteSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        public async Task<SpotOrderSyncSettingDto> Handle(DeleteSyncSettingCommand request, CancellationToken cancellationToken)
        {
            var entity = await applicationDbContext.SpotOrderSyncSettings
                .FirstOrDefaultAsync(x => x.Symbol == request.Symbol && x.UserId == currentUser.Id, cancellationToken)
                ?? throw new NotFoundException($"{request.Symbol} is not found.");

            applicationDbContext.SpotOrderSyncSettings.Remove(entity);
            await applicationDbContext.SaveChangesAsync(cancellationToken);

            return SpotOrderSyncSettingDto.From(entity);
        }
    }
}
