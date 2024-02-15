using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class BnbSettingConfiguration : IEntityTypeConfiguration<BnbSetting>
    {
        public void Configure(EntityTypeBuilder<BnbSetting> builder)
        {
            // Guid max length
            builder.HasKey(t => t.UserId);
        }
    }
}
