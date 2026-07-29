using Cex.Application.Common.Abstractions;
using Cex.Application.Grid.DTOs;
using Lib.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Grid.Commands.DeleteSpotGrid
{
    public record DeleteSpotGridCommand(long Id) : IRequest<SpotGridDto>
    {
    }

    public class DeleteSpotGridCommandHandler(ICurrentUser currentUser, ICexDbContext cexDbContext)
        : IRequestHandler<DeleteSpotGridCommand, SpotGridDto>
    {
        public async Task<SpotGridDto> Handle(DeleteSpotGridCommand command, CancellationToken cancellationToken)
        {
            var entity = await cexDbContext.SpotGrids
                .FirstOrDefaultAsync(x => x.Id == command.Id && x.UserId == currentUser.Id, cancellationToken);

            if (entity == null)
            {
                return new SpotGridDto();
            }

            entity.DeletedAt = DateTime.UtcNow;

            cexDbContext.SpotGrids.Update(entity);
            await cexDbContext.SaveChangesAsync(cancellationToken);

            return SpotGridDto.From(entity);
        }
    }
}
