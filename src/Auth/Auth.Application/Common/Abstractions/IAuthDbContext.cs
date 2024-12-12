using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Common.Abstractions
{
    public interface IAuthDbContext
    {
        DbSet<UserLoginHistory> UserLoginHistories { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}