using Cex.Application.Common.Abstractions;
using Cex.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cex.Infrastructure.Data
{
    public class CexDbContext(DbContextOptions<CexDbContext> options) : DbContext(options), ICexDbContext
    {
        public DbSet<Config> Configs => Set<Config>();
        public DbSet<Candle> Candles => Set<Candle>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            CustomizeIdentityTableNames(builder);
        }

        public new Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            return base.SaveChangesAsync(cancellationToken);
        }

        static void CustomizeIdentityTableNames(ModelBuilder builder)
        {
            builder.Entity<Config>(entity =>
            {
                entity.HasKey(c => c.Key);
            });

            builder.Entity<Candle>(entity =>
            {
                entity.HasKey(c => c.Session);
                entity.Property(t => t.Session).ValueGeneratedNever();

                entity.Property(t => t.OpenPrice)
                .HasPrecision(8, 3)
                .IsRequired();

                entity.Property(t => t.ClosePrice)
                .HasPrecision(8, 3)
                .IsRequired();

                entity.Property(t => t.HighPrice)
                .HasPrecision(8, 3)
                .IsRequired();

                entity.Property(t => t.LowPrice)
                .HasPrecision(8, 3)
                .IsRequired();

                entity.Property(t => t.BaseVolume)
                .HasPrecision(8, 3)
                .IsRequired();

                entity.Property(t => t.QuoteVolume)
                .HasPrecision(13, 3)
                .IsRequired();
            });
        }
    }
}
