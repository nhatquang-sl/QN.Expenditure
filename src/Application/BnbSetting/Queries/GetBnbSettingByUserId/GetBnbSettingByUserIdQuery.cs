using Application.Common.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSetting.Queries.GetBnbSettingByUserId
{
    public record GetBnbSettingByUserIdQuery() : IRequest<Domain.Entities.BnbSetting>;

    public class GetBnbSettingByUserIdQueryHandler(ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<GetBnbSettingByUserIdQuery, Domain.Entities.BnbSetting>
    {
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<Domain.Entities.BnbSetting> Handle(GetBnbSettingByUserIdQuery request, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.BnbSettings.FirstOrDefaultAsync(x => x.UserId == _currentUser.Id, cancellationToken);

            return entity ?? default;
        }
    }
}
