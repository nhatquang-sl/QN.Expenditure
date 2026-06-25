using Cex.Application.BnbSetting.DTOs;
using Cex.Application.Common.Abstractions;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.BnbSetting.Commands.UpdateBnbSetting
{
    public record UpdateBnbSettingCommand(string ApiKey, string SecretKey) : IRequest<BnbSettingDto>;

    public class UpdateBnbSettingCommandHandler(ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<UpdateBnbSettingCommand, BnbSettingDto>
    {
        public async Task<BnbSettingDto> Handle(UpdateBnbSettingCommand request, CancellationToken cancellationToken)
        {
            var entity =
                await cexDbContext.BnbSettings.FirstOrDefaultAsync(x => x.UserId == currentUser.Id,
                    cancellationToken);
            if (entity == null)
            {
                entity = new Domain.Entities.BnbSetting
                {
                    UserId = currentUser.Id,
                    ApiKey = request.ApiKey,
                    SecretKey = request.SecretKey
                };
                cexDbContext.BnbSettings.Add(entity);
            }
            else
            {
                entity.ApiKey = request.ApiKey;
                entity.SecretKey = request.SecretKey;
                cexDbContext.BnbSettings.Update(entity);
            }

            await cexDbContext.SaveChangesAsync(cancellationToken);

            return BnbSettingDto.From(entity);
        }
    }
}
