using System.Reflection;
using Cex.Application.Common.Abstractions;
using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cex.Infrastructure.Data
{
    public class CexDbContext : DbContext, ICexDbContext
    {
        public CexDbContext(DbContextOptions<CexDbContext> options) : base(options)
        {
            // ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public DbSet<SpotOrderSyncSetting> SpotOrderSyncSettings { get; }
        public DbSet<SpotOrder> SpotOrders { get; }
        public DbSet<BnbSetting> BnbSettings { get; }
        public DbSet<SpotGrid> SpotGrids { get; init; }
        public DbSet<SpotGridStep> SpotGridSteps { get; init; }

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