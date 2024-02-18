using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Queries.GetSyncSettings
{
    public record GetSyncSettingsQuery : IRequest<List<SpotOrderSyncSettingDto>>;

    public class GetSyncSettingsQueryHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<GetSyncSettingsQuery, List<SpotOrderSyncSettingDto>>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<List<SpotOrderSyncSettingDto>> Handle(GetSyncSettingsQuery request, CancellationToken cancellationToken)
        {
            var settings = await _applicationDbContext.SpotOrderSyncSettings
                .Where(x => x.UserId == _currentUser.Id)
                .ToListAsync(cancellationToken);

            return _mapper.Map<List<SpotOrderSyncSettingDto>>(settings) ?? [];
        }
    }
}
