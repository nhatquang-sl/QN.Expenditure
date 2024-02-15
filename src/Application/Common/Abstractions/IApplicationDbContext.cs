using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Abstractions
{
    public interface IApplicationDbContext
    {
        DbSet<UserLoginHistory> UserLoginHistories { get; }
        DbSet<SpotOrderSyncSetting> SpotOrderSyncSettings { get; }
        DbSet<SpotOrder> SpotOrders { get; }
        DbSet<Domain.Entities.BnbSetting> BnbSettings { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
