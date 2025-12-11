using Cex.Application.Common.Abstractions;
using Cex.Application.Sync.SyncSetting.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Sync.SyncSetting.Queries.GetSyncSettings;

public record GetSyncSettingsQuery : IRequest<List<SyncSettingDto>>;

public class GetSyncSettingsQueryHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<GetSyncSettingsQuery, List<SyncSettingDto>>
{
    public async Task<List<SyncSettingDto>> Handle(GetSyncSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await cexDbContext.SyncSettings
            .Where(x => x.UserId == currentUser.Id)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        return entities.Select(x => new SyncSettingDto(x)).ToList();
    }
}
