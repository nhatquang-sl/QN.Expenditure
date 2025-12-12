using System.Reflection;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cex.Infrastructure.Data
{
    public class CexDbContext(DbContextOptions<CexDbContext> options) : DbContext(options), ICexDbContext
    {
        // ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        public DbSet<SpotOrderSyncSetting> SpotOrderSyncSettings { get; init; }
        public DbSet<SpotOrder> SpotOrders { get; init; }
        public DbSet<BnbSetting> BnbSettings { get; init; }
        public DbSet<ExchangeSetting> ExchangeSettings { get; init; }
        public DbSet<SyncSetting> SyncSettings { get; init; }
        public DbSet<SpotGrid> SpotGrids { get; init; }
        public DbSet<SpotGridStep> SpotGridSteps { get; init; }
        public DbSet<TradeHistory> TradeHistories { get; init; }

        public new Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            CustomizeIdentityTableNames(builder);
        }

        private static void CustomizeIdentityTableNames(ModelBuilder builder)
        {
        }
    }
}