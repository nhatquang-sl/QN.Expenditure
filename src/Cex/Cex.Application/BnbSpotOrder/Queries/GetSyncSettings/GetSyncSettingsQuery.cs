using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Queries.GetSyncSettings
{
    public record GetSyncSettingsQuery : IRequest<List<SpotOrderSyncSettingDto>>;

    public class GetSyncSettingsQueryHandler(
        ICurrentUser currentUser,
        ICexDbContext cexDbContext)
        : IRequestHandler<GetSyncSettingsQuery, List<SpotOrderSyncSettingDto>>
    {
        public async Task<List<SpotOrderSyncSettingDto>> Handle(GetSyncSettingsQuery request,
            CancellationToken cancellationToken)
        {
            var settings = await cexDbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == currentUser.Id)
                .ToListAsync(cancellationToken);

            return settings.Select(SpotOrderSyncSettingDto.From).ToList();
        }
    }
}
