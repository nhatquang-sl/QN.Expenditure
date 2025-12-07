using Cex.Application.Common.Abstractions;
using Cex.Application.Settings.ExchangeSetting.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Settings.ExchangeSetting.Queries.GetExchangeSettings;

public record GetExchangeSettingsQuery : IRequest<List<ExchangeSettingDto>>;

public class GetExchangeSettingsQueryHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<GetExchangeSettingsQuery, List<ExchangeSettingDto>>
{
    public async Task<List<ExchangeSettingDto>> Handle(GetExchangeSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var entities = await cexDbContext.ExchangeSettings
            .Where(x => x.UserId == currentUser.Id)
            .OrderBy(x => x.ExchangeName)
            .ToListAsync(cancellationToken);

        return entities.Select(x => new ExchangeSettingDto(x)).ToList();
    }
}
