using Cex.Application.Common.Abstractions;
using Cex.Application.Sync.SyncSetting.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Sync.SyncSetting.Commands.UpsertSyncSetting;

public record UpsertSyncSettingCommand(
    string Symbol,
    long StartSync  // Unix timestamp in milliseconds
) : IRequest<SyncSettingDto>;

public class UpsertSyncSettingCommandHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<UpsertSyncSettingCommand, SyncSettingDto>
{
    public async Task<SyncSettingDto> Handle(UpsertSyncSettingCommand request, CancellationToken cancellationToken)
    {
        var entity = await cexDbContext.SyncSettings
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.Symbol == request.Symbol,
                cancellationToken);

        // Convert Unix timestamp (milliseconds) to DateTime
        var startSyncDateTime = DateTimeOffset.FromUnixTimeMilliseconds(request.StartSync).UtcDateTime;

        if (entity == null)
        {
            // Create: Set LastSync = StartSync
            entity = new Domain.Entities.SyncSetting
            {
                UserId = currentUser.Id,
                Symbol = request.Symbol,
                StartSync = startSyncDateTime,
                LastSync = startSyncDateTime  // Auto-set to StartSync
            };
            cexDbContext.SyncSettings.Add(entity);
        }
        else
        {
            // Update: Only update StartSync, preserve LastSync
            entity.StartSync = startSyncDateTime;
            cexDbContext.SyncSettings.Update(entity);
        }

        await cexDbContext.SaveChangesAsync(cancellationToken);

        return new SyncSettingDto(entity);
    }
}
