using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cex.Infrastructure.Data.Configurations
{
    public class ExchangeConfigConfiguration : IEntityTypeConfiguration<ExchangeConfig>
    {
        public void Configure(EntityTypeBuilder<ExchangeConfig> builder)
        {
            builder.HasKey(t => new { t.UserId, t.ExchangeName });

            builder.Property(t => t.UserId)
                .HasMaxLength(36)
                .IsRequired();

            builder.Property(t => t.ExchangeName)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(t => t.ApiKey)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(t => t.Secret)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(t => t.Passphrase)
                .HasMaxLength(500);

            builder.HasIndex(t => t.UserId);
        }
    }
}
