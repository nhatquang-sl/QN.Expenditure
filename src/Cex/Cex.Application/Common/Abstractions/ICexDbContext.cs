using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Common.Abstractions
{
    public interface ICexDbContext
    {
        DbSet<SpotOrderSyncSetting> SpotOrderSyncSettings { get; init; }
        DbSet<SpotOrder> SpotOrders { get; init; }
        DbSet<Domain.Entities.BnbSetting> BnbSettings { get; init; }
        DbSet<SpotGrid> SpotGrids { get; init; }
        DbSet<SpotGridStep> SpotGridSteps { get; init; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}