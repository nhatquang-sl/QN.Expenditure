using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations;

public class SyncSettingConfiguration : IEntityTypeConfiguration<SyncSetting>
{
    public void Configure(EntityTypeBuilder<SyncSetting> builder)
    {
        builder.HasKey(t => new { t.UserId, t.Symbol });

        builder.Property(t => t.UserId)
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(t => t.Symbol)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.StartSync)
            .IsRequired();

        builder.Property(t => t.LastSync)
            .IsRequired();

        builder.HasIndex(t => t.UserId);
    }
}
