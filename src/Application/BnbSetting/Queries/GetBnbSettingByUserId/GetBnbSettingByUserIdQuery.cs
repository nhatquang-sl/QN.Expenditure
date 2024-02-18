using Application.BnbSetting.DTOs;
using Application.Common.Abstractions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSetting.Queries.GetBnbSettingByUserId
{
    public record GetBnbSettingByUserIdQuery() : IRequest<BnbSettingDto>;

    public class GetBnbSettingByUserIdQueryHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<GetBnbSettingByUserIdQuery, BnbSettingDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<BnbSettingDto> Handle(GetBnbSettingByUserIdQuery request, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.BnbSettings.FirstOrDefaultAsync(x => x.UserId == _currentUser.Id, cancellationToken);

            return _mapper.Map<BnbSettingDto>(entity) ?? new BnbSettingDto();
        }
    }
}
