using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Abstractions
{
    public interface IApplicationDbContext
    {
        DbSet<UserLoginHistory> UserLoginHistories { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
