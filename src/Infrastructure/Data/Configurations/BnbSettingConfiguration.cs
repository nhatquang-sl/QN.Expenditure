using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class BnbSettingConfiguration : IEntityTypeConfiguration<BnbSetting>
    {
        public void Configure(EntityTypeBuilder<BnbSetting> builder)
        {
            builder.HasKey(t => t.UserId);

            builder.Property(t => t.ApiKey)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(t => t.SecretKey)
                .HasMaxLength(500)
                .IsRequired();
        }
    }
}
