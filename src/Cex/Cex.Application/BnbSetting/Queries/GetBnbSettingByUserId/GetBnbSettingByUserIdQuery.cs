using Cex.Application.BnbSetting.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSetting.Queries.GetBnbSettingByUserId
{
    public record GetBnbSettingByUserIdQuery : IRequest<BnbSettingDto>;

    public class GetBnbSettingByUserIdQueryHandler(
        ICurrentUser currentUser,
        ICexDbContext cexDbContext)
        : IRequestHandler<GetBnbSettingByUserIdQuery, BnbSettingDto>
    {
        public async Task<BnbSettingDto> Handle(GetBnbSettingByUserIdQuery request, CancellationToken cancellationToken)
        {
            var entity = await cexDbContext.BnbSettings
                .Where(x => x.UserId == currentUser.Id)
                .Select(x => new BnbSettingDto
                {
                    ApiKey = x.ApiKey,
                    SecretKey = x.SecretKey
                })
                .FirstOrDefaultAsync(cancellationToken);

            return entity ?? new BnbSettingDto();
        }
    }
}
