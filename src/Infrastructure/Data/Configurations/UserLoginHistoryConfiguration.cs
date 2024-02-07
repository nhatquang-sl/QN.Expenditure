using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class UserLoginHistoryConfiguration : IEntityTypeConfiguration<UserLoginHistory>
    {
        public void Configure(EntityTypeBuilder<UserLoginHistory> builder)
        {
            // Guid max length
            builder.Property(t => t.UserId)
                .HasMaxLength(36);

            // IPv6 max length
            builder.Property(t => t.IpAddress)
                .HasMaxLength(39);

            builder.Property(t => t.AccessToken)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(t => t.RefreshToken)
                .HasMaxLength(500)
                .IsRequired();
        }
    }
}
