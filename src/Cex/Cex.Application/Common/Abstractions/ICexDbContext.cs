using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Common.Abstractions
{
    public interface ICexDbContext
    {
        DbSet<SpotOrderSyncSetting> SpotOrderSyncSettings { get; }
        DbSet<SpotOrder> SpotOrders { get; }
        DbSet<Domain.Entities.BnbSetting> BnbSettings { get; }
        DbSet<SpotGrid> SpotGrids { get; }
        DbSet<SpotGridStep> SpotGridSteps { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}