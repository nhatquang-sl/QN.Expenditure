using Cex.Application.Common.Abstractions;
using Cex.Application.ExchangeConfigs.DTOs;
using Cex.Domain.Entities;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.ExchangeConfigs.Commands.DeleteExchangeConfig;

public record DeleteExchangeConfigCommand(ExchangeName ExchangeName) : IRequest;

public class DeleteExchangeConfigCommandHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<DeleteExchangeConfigCommand>
{
    public async Task Handle(DeleteExchangeConfigCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await cexDbContext.ExchangeConfigs
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.ExchangeName == request.ExchangeName,
                cancellationToken);

        if (entity != null)
        {
            cexDbContext.ExchangeConfigs.Remove(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}