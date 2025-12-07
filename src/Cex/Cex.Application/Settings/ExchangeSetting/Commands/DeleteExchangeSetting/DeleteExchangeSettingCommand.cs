using Cex.Application.Common.Abstractions;
using Cex.Domain.Enums;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Settings.ExchangeSetting.Commands.DeleteExchangeSetting;

public record DeleteExchangeSettingCommand(ExchangeName ExchangeName) : IRequest;

public class DeleteExchangeSettingCommandHandler(
    ICurrentUser currentUser,
    ICexDbContext cexDbContext)
    : IRequestHandler<DeleteExchangeSettingCommand>
{
    public async Task Handle(DeleteExchangeSettingCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await cexDbContext.ExchangeSettings
            .FirstOrDefaultAsync(x => x.UserId == currentUser.Id && x.ExchangeName == request.ExchangeName,
                cancellationToken);

        if (entity != null)
        {
            cexDbContext.ExchangeSettings.Remove(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
