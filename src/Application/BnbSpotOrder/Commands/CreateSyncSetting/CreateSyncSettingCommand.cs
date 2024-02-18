using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using Application.Common.Extensions;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.BnbSpotOrder.Commands.CreateSyncSetting
{
    public record CreateSyncSettingCommand(string Symbol, long LastSyncAt) : IRequest<SpotOrderSyncSettingDto>;

    public class CreateSyncSettingCommandHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<CreateSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<SpotOrderSyncSettingDto> Handle(CreateSyncSettingCommand request, CancellationToken cancellationToken)
        {
            var entity = new SpotOrderSyncSetting
            {
                UserId = _currentUser.Id,
                Symbol = request.Symbol,
                LastSyncAt = request.LastSyncAt.ToDateTimeFromMilliseconds()
            };

            _applicationDbContext.SpotOrderSyncSettings.Add(entity);
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotOrderSyncSettingDto>(entity) ?? new SpotOrderSyncSettingDto();
        }
    }
}
