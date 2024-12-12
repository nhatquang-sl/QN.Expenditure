using AutoMapper;
using Cex.Application.BnbSpotOrder.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using Lib.Application.Exceptions;
using Lib.Application.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSpotOrder.Commands.UpdateSyncSetting
{
    public record UpdateSyncSettingCommand(string Symbol, long LastSyncAt) : IRequest<SpotOrderSyncSettingDto>;

    public class UpdateSyncSettingCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext dbContext)
        : IRequestHandler<UpdateSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly ICexDbContext _dbContext = dbContext;
        private readonly IMapper _mapper = mapper;

        public async Task<SpotOrderSyncSettingDto> Handle(UpdateSyncSettingCommand request,
            CancellationToken cancellationToken)
        {
            var entity =
                await _dbContext.SpotOrderSyncSettings.FirstOrDefaultAsync(
                    x => x.Symbol == request.Symbol && x.UserId == _currentUser.Id, cancellationToken)
                ?? throw new NotFoundException($"{request.Symbol} is not found.");

            entity.LastSyncAt = request.LastSyncAt.ToDateTimeFromMilliseconds();

            _dbContext.SpotOrderSyncSettings.Update(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotOrderSyncSettingDto>(entity) ?? new SpotOrderSyncSettingDto();
        }
    }
}