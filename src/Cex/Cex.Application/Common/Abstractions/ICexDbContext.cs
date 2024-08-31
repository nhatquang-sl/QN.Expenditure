using Microsoft.EntityFrameworkCore;

namespace Cex.Application.Common.Abstractions
{
    public interface ICexDbContext
    {
        DbSet<Domain.Config> Configs { get; }
        DbSet<Domain.Candle> Candles { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
