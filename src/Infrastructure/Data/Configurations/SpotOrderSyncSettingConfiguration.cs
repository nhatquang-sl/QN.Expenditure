using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    internal class SpotOrderSyncSettingConfiguration : IEntityTypeConfiguration<SpotOrderSyncSetting>
    {
        public void Configure(EntityTypeBuilder<SpotOrderSyncSetting> builder)
        {
            // Guid max length
            builder.Property(t => t.UserId)
                .HasMaxLength(36);

            builder.Property(t => t.Symbol)
                .HasMaxLength(10)
                .IsRequired();

            builder.HasKey(t => new { t.Symbol, t.UserId });
        }
    }
}
