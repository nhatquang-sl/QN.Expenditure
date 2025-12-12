using AutoMapper;
using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Queries.GetSyncSettings
{
    public record GetSyncSettingsQuery : IRequest<List<SpotOrderSyncSettingDto>>;

    public class GetSyncSettingsQueryHandler(
        IMapper mapper,
        ICurrentUser currentUser,
        ICexDbContext cexDbContext)
        : IRequestHandler<GetSyncSettingsQuery, List<SpotOrderSyncSettingDto>>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<List<SpotOrderSyncSettingDto>> Handle(GetSyncSettingsQuery request,
            CancellationToken cancellationToken)
        {
            var settings = await _cexDbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == _currentUser.Id)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotOrderSyncSettingDto>>(settings) ?? [];
        }
    }
}