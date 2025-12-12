using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class BnbSettingConfiguration : IEntityTypeConfiguration<BnbSetting>
    {
        public void Configure(EntityTypeBuilder<BnbSetting> builder)
        {
            builder.HasKey(t => t.UserId);
        }
    }
}