using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Infrastructure.Repositories;

public class MarketPriceRepository : IMarketPriceRepository
{
    private readonly MarketDataDbContext _dbContext;
    private readonly ILogger<MarketPriceRepository> _logger;

    public MarketPriceRepository(MarketDataDbContext dbContext, ILogger<MarketPriceRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MarketPrice?> GetByProductNameAsync(string productName, CancellationToken ct)
    {
        try
        {
            return await _dbContext.MarketPrices
                .AsNoTracking()
                .FirstOrDefaultAsync(mp =>
                    mp.ProductName.ToLower() == productName.ToLower() && mp.IsActive,
                    ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL unavailable while getting MarketPrice by productName={ProductName}", productName);
            return null;
        }
    }

    public async Task AddAsync(MarketPrice marketPrice, CancellationToken ct)
    {
        try
        {
            await _dbContext.MarketPrices.AddAsync(marketPrice, ct);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL unavailable while adding MarketPrice productName={ProductName}", marketPrice.ProductName);
        }
    }

    public async Task UpdateAsync(MarketPrice marketPrice, CancellationToken ct)
    {
        try
        {
            _dbContext.MarketPrices.Update(marketPrice);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL unavailable while updating MarketPrice id={Id}", marketPrice.Id);
        }
    }

    public async Task<IEnumerable<MarketPrice>> GetAllAsync(CancellationToken ct)
    {
        try
        {
            return await _dbContext.MarketPrices
                .AsNoTracking()
                .Where(mp => mp.IsActive)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL unavailable while getting all MarketPrices");
            return Array.Empty<MarketPrice>();
        }
    }

    public async Task<IReadOnlyList<MarketPrice>> GetActiveByCategoryAndRegionAsync(
        string expenseCategory,
        string geographicRegion,
        CancellationToken ct)
    {
        var cat = expenseCategory.Trim().ToLower();
        var reg = geographicRegion.Trim().ToLower();

        try
        {
            return await _dbContext.MarketPrices
                .AsNoTracking()
                .Where(mp =>
                    mp.IsActive &&
                    mp.ExpenseCategory.ToLower() == cat &&
                    mp.GeographicRegion.ToLower() == reg)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL unavailable while getting MarketPrices by category={Category} region={Region}", expenseCategory, geographicRegion);
            return Array.Empty<MarketPrice>();
        }
    }
}