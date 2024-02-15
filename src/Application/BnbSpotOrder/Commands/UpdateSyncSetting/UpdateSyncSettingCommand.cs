using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using Application.Common.Extensions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.UpdateSyncSetting
{
    public record UpdateSyncSettingCommand(string Symbol, long LastSyncAt) : IRequest<SpotOrderSyncSettingDto>;

    public class UpdateSyncSettingCommandHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<UpdateSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<SpotOrderSyncSettingDto> Handle(UpdateSyncSettingCommand request, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.SpotOrderSyncSettings.FirstOrDefaultAsync(x => x.Symbol == request.Symbol && x.UserId == _currentUser.Id, cancellationToken)
                ?? throw new NotFoundException($"{request.Symbol} is not found.");

            entity.LastSyncAt = request.LastSyncAt.ToDateTimeFromMilliseconds();

            _applicationDbContext.SpotOrderSyncSettings.Update(entity);
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotOrderSyncSettingDto>(entity) ?? new SpotOrderSyncSettingDto();
        }
    }
}
