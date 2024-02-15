using Application.BnbSpotOrder.DTOs;
using Application.Common.Abstractions;
using Application.Common.Exceptions;
using AutoMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.BnbSpotOrder.Commands.DeleteSyncSetting
{
    public record DeleteSyncSettingCommand(string Symbol) : IRequest<SpotOrderSyncSettingDto>;

    public class DeleteSyncSettingCommandHandler(IMapper mapper, ICurrentUser currentUser, IApplicationDbContext applicationDbContext)
        : IRequestHandler<DeleteSyncSettingCommand, SpotOrderSyncSettingDto>
    {
        private readonly IMapper _mapper = mapper;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IApplicationDbContext _applicationDbContext = applicationDbContext;

        public async Task<SpotOrderSyncSettingDto> Handle(DeleteSyncSettingCommand request, CancellationToken cancellationToken)
        {
            var entity = await _applicationDbContext.SpotOrderSyncSettings.FirstOrDefaultAsync(x => x.Symbol == request.Symbol && x.UserId == _currentUser.Id, cancellationToken)
                ?? throw new NotFoundException($"{request.Symbol} is not found.");

            _applicationDbContext.SpotOrderSyncSettings.Remove(entity);
            await _applicationDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<SpotOrderSyncSettingDto>(entity) ?? new SpotOrderSyncSettingDto();
        }
    }
}
