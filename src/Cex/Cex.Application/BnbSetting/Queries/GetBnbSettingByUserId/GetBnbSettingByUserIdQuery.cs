using AutoMapper;
using Cex.Application.BnbSetting.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSetting.Queries.GetBnbSettingByUserId
{
    public record GetBnbSettingByUserIdQuery : IRequest<BnbSettingDto>;

    public class GetBnbSettingByUserIdQueryHandler(
        IMapper mapper,
        ICurrentUser currentUser,
        ICexDbContext cexDbContext)
        : IRequestHandler<GetBnbSettingByUserIdQuery, BnbSettingDto>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<BnbSettingDto> Handle(GetBnbSettingByUserIdQuery request, CancellationToken cancellationToken)
        {
            var entity =
                await _cexDbContext.BnbSettings.FirstOrDefaultAsync(x => x.UserId == _currentUser.Id,
                    cancellationToken);

            return _mapper.Map<BnbSettingDto>(entity) ?? new BnbSettingDto();
        }
    }
}