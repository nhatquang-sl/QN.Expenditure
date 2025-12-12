using System.Reflection;
using Auth.Application.Common.Abstractions;
using Auth.Domain.Entities;
using Auth.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Data
{
    public class AuthDbContext(DbContextOptions<AuthDbContext> options)
        : IdentityDbContext<ApplicationUser>(options), IAuthDbContext
    {
        public DbSet<UserLoginHistory> UserLoginHistories { get; init; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            base.OnModelCreating(builder);

            CustomizeIdentityTableNames(builder);
        }

        private static void CustomizeIdentityTableNames(ModelBuilder builder)
        {
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.ToTable("Users");
                entity.Property(e => e.FirstName).HasMaxLength(50);
                entity.Property(e => e.LastName).HasMaxLength(50);
            });

            builder.Entity<IdentityRole>(entity => { entity.ToTable("Roles"); });

            builder.Entity<IdentityUserRole<string>>(entity => { entity.ToTable("UserRoles"); });

            builder.Entity<IdentityUserToken<string>>(entity => { entity.ToTable("UserTokens"); });

            builder.Entity<IdentityUserLogin<string>>(entity => { entity.ToTable("UserLogins"); });

            builder.Entity<IdentityUserClaim<string>>(entity => { entity.ToTable("UserClaims"); });

            builder.Entity<IdentityRoleClaim<string>>(entity => { entity.ToTable("RoleClaims"); });
        }
    }
}