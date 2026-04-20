using Microsoft.EntityFrameworkCore;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Infrastructure.Repositories;

public class MarketPriceRepository : IMarketPriceRepository
{
    private readonly MarketDataDbContext _dbContext;

    public MarketPriceRepository(MarketDataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MarketPrice?> GetByProductNameAsync(string productName, CancellationToken ct)
    {
        return await _dbContext.MarketPrices
            .AsNoTracking()
            .FirstOrDefaultAsync(mp =>
                mp.ProductName.ToLower() == productName.ToLower() && mp.IsActive,
                ct);
    }

    public async Task AddAsync(MarketPrice marketPrice, CancellationToken ct)
    {
        await _dbContext.MarketPrices.AddAsync(marketPrice, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MarketPrice marketPrice, CancellationToken ct)
    {
        _dbContext.MarketPrices.Update(marketPrice);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<MarketPrice>> GetAllAsync(CancellationToken ct)
    {
        return await _dbContext.MarketPrices
            .AsNoTracking()
            .Where(mp => mp.IsActive)
            .ToListAsync(ct);
    }
}