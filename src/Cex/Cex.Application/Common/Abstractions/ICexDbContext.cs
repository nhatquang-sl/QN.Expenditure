using Cex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using BnbSettingEntity = Cex.Domain.Entities.BnbSetting;

namespace Cex.Application.Common.Abstractions
{
    public interface ICexDbContext
    {
        DbSet<SpotOrderSyncSetting> SpotOrderSyncSettings { get; init; }
        DbSet<SpotOrder> SpotOrders { get; init; }
        DbSet<BnbSettingEntity> BnbSettings { get; init; }
        DbSet<ExchangeSetting> ExchangeSettings { get; init; }
        DbSet<SpotGrid> SpotGrids { get; init; }
        DbSet<SpotGridStep> SpotGridSteps { get; init; }
        DbSet<TradeHistory> TradeHistories { get; init; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}