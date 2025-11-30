using Cex.Application.Common.Abstractions;
using Cex.Application.ExchangeConfigs.DTOs;
using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.ExchangeConfigs.Commands.UpsertExchangeConfig
{
    public record UpsertExchangeConfigCommand(
        ExchangeName ExchangeName,
        string ApiKey,
        string Secret,
        string? Passphrase = null
    ) : IRequest<ExchangeConfigDto>;

    public class UpsertExchangeConfigCommandHandler(
        ICurrentUser currentUser,
        ICexDbContext cexDbContext)
        : IRequestHandler<UpsertExchangeConfigCommand, ExchangeConfigDto>
    {
        public async Task<ExchangeConfigDto> Handle(UpsertExchangeConfigCommand request, CancellationToken cancellationToken)
        {
            var entity = await cexDbContext.ExchangeConfigs
                .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.ExchangeName == request.ExchangeName,
                    cancellationToken);

            if (entity == null)
            {
                foreach (ExchangeName exchangeName in Enum.GetValues<ExchangeName>())
                {
                    entity = new ExchangeConfig
                    {
                        UserId = currentUser.Id,
                        ExchangeName = exchangeName,
                        ApiKey = request.ApiKey,
                        Secret = request.Secret,
                        Passphrase = request.Passphrase
                    };

                    cexDbContext.ExchangeConfigs.Add(entity);
                }
            }
            else
            {
                entity.ApiKey = request.ApiKey;
                entity.Secret = request.Secret;
                entity.Passphrase = request.Passphrase;
                cexDbContext.ExchangeConfigs.Update(entity);
            }

            await cexDbContext.SaveChangesAsync(cancellationToken);

            return new ExchangeConfigDto(entity);
        }
    }
}
