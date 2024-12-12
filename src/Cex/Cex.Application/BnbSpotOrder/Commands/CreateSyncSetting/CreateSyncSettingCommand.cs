using AutoMapper;
using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Lib.Application.Abstractions;
using Lib.Application.Extensions;
using MediatR;

namespace Cex.Application.BnbSpotOrder.Commands.CreateSyncSetting
{
    public record CreateSyncSettingCommand(string Symbol, long LastSyncAt) : IRequest<SpotOrderSyncSettingDto>;

    public class CreateSyncSettingCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<CreateSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<SpotOrderSyncSettingDto> Handle(CreateSyncSettingCommand request,
            CancellationToken cancellationToken)
        {
            var entity = new SpotOrderSyncSetting
            {
                UserId = _currentUser.Id,
                Symbol = request.Symbol,
                LastSyncAt = request.LastSyncAt.ToDateTimeFromMilliseconds()
            };

            _cexDbContext.SpotOrderSyncSettings.Add(entity);
            await _cexDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotOrderSyncSettingDto>(entity) ?? new SpotOrderSyncSettingDto();
        }
    }
}