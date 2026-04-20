using Microsoft.EntityFrameworkCore;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Infrastructure.Repositories;

public class MarketPriceHistoryRepository : IMarketPriceHistoryRepository
{
    private readonly MarketDataDbContext _dbContext;

    public MarketPriceHistoryRepository(MarketDataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(MarketPriceHistory history, CancellationToken ct)
    {
        await _dbContext.MarketPriceHistory.AddAsync(history, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<MarketPriceHistory>> GetByProductNameAsync(string productName, int days, CancellationToken ct)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        return await _dbContext.MarketPriceHistory
            .AsNoTracking()
            .Where(h => h.ProductName.ToLower() == productName.ToLower() &&
                       h.CollectedDate >= startDate)
            .OrderByDescending(h => h.CollectedDate)
            .ToListAsync(ct);
    }
}