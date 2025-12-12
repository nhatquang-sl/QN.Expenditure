using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class SpotOrderSyncSettingConfiguration : IEntityTypeConfiguration<SpotOrderSyncSetting>
    {
        public void Configure(EntityTypeBuilder<SpotOrderSyncSetting> builder)
        {
            builder.HasKey(k => new { k.UserId, k.Symbol });
        }
    }
}