using AutoMapper;
using Cex.Application.BnbSetting.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSetting.Commands.UpdateBnbSetting
{
    public record UpdateBnbSettingCommand(string ApiKey, string SecretKey) : IRequest<BnbSettingDto>;

    public class UpdateBnbSettingCommandHandler(IMapper mapper, ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<UpdateBnbSettingCommand, BnbSettingDto>
    {
        private readonly ICexDbContext _cexDbContext = cexDbContext;
        private readonly ICurrentUser _currentUser = currentUser;
        private readonly IMapper _mapper = mapper;

        public async Task<BnbSettingDto> Handle(UpdateBnbSettingCommand request, CancellationToken cancellationToken)
        {
            var entity =
                await _cexDbContext.BnbSettings.FirstOrDefaultAsync(x => x.UserId == _currentUser.Id,
                    cancellationToken);
            if (entity == null)
            {
                entity = new Domain.Entities.BnbSetting
                {
                    UserId = _currentUser.Id,
                    ApiKey = request.ApiKey,
                    SecretKey = request.SecretKey
                };
                _cexDbContext.BnbSettings.Add(entity);
            }
            else
            {
                entity.ApiKey = request.ApiKey;
                entity.SecretKey = request.SecretKey;
                _cexDbContext.BnbSettings.Update(entity);
            }

            await _cexDbContext.SaveChangesAsync(cancellationToken);

            return _mapper.Map<BnbSettingDto>(entity) ?? new BnbSettingDto();
        }
    }
}