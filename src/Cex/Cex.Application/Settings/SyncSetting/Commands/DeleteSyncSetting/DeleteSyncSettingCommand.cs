using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Sync.SyncSetting.Commands.DeleteSyncSetting;

public record DeleteSyncSettingCommand(string Symbol) : IRequest;

public class DeleteSyncSettingCommandHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<DeleteSyncSettingCommand>
{
    public async Task Handle(DeleteSyncSettingCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await cexDbContext.SyncSettings
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.Symbol == request.Symbol,
                cancellationToken);

        if (entity != null)
        {
            cexDbContext.SyncSettings.Remove(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
