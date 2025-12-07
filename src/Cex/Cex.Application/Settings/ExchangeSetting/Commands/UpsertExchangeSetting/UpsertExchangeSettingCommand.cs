using Cex.Application.Common.Abstractions;
using Cex.Application.Settings.ExchangeSetting.DTOs;
using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Settings.ExchangeSetting.Commands.UpsertExchangeSetting;

public record UpsertExchangeSettingCommand(
    ExchangeName ExchangeName,
    string ApiKey,
    string Secret,
    string? Passphrase = null
) : IRequest<ExchangeSettingDto>;

public class UpsertExchangeSettingCommandHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<UpsertExchangeSettingCommand, ExchangeSettingDto>
{
    public async Task<ExchangeSettingDto> Handle(UpsertExchangeSettingCommand request, CancellationToken cancellationToken)
    {
        var entity = await cexDbContext.ExchangeSettings
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.ExchangeName == request.ExchangeName,
                cancellationToken);

        if (entity == null)
        {
            entity = new Domain.Entities.ExchangeSetting
            {
                UserId = currentUser.Id,
                ExchangeName = request.ExchangeName,
                ApiKey = request.ApiKey,
                Secret = request.Secret,
                Passphrase = request.Passphrase
            };
            cexDbContext.ExchangeSettings.Add(entity);
        }
        else
        {
            entity.ApiKey = request.ApiKey;
            entity.Secret = request.Secret;
            entity.Passphrase = request.Passphrase;
            cexDbContext.ExchangeSettings.Update(entity);
        }

        await cexDbContext.SaveChangesAsync(cancellationToken);

        return new ExchangeSettingDto(entity);
    }
}
