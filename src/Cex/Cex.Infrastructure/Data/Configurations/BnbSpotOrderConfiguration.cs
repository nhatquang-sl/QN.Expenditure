using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class BnbSpotOrderConfiguration : IEntityTypeConfiguration<SpotOrder>
    {
        public void Configure(EntityTypeBuilder<SpotOrder> builder)
        {
            builder.HasKey(t => t.OrderId);

            builder.Property(t => t.Price)
                .HasPrecision(13, 6);
            // .IsRequired();

            builder.Property(t => t.OrigQty)
                .HasPrecision(18, 6);
            // .IsRequired();

            builder.Property(t => t.Fee)
                .HasPrecision(18, 10);
            //     .IsRequired();
        }
    }
}