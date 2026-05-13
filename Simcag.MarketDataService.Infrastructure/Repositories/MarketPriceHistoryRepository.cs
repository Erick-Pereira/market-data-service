using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Infrastructure.Repositories;

public class MarketPriceHistoryRepository : IMarketPriceHistoryRepository
{
    private readonly MarketDataDbContext _dbContext;
    private readonly ILogger<MarketPriceHistoryRepository> _logger;

    public MarketPriceHistoryRepository(MarketDataDbContext dbContext, ILogger<MarketPriceHistoryRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task AddAsync(MarketPriceHistory history, CancellationToken ct)
    {
        try
        {
            await _dbContext.MarketPriceHistory.AddAsync(history, ct);
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug(
                "MarketPriceHistory gravado: productName={ProductName}, price={Price}, sourceLen={Len}",
                history.ProductName,
                history.Price,
                history.Source.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add MarketPriceHistory productName={ProductName}", history.ProductName);
        }
    }

    public async Task<IEnumerable<MarketPriceHistory>> GetByProductNameAsync(string productName, int days, CancellationToken ct)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);

        try
        {
            return await _dbContext.MarketPriceHistory
                .AsNoTracking()
                .Where(h => h.ProductName.ToLower() == productName.ToLower() &&
                           h.CollectedDate >= startDate)
                .OrderByDescending(h => h.CollectedDate)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read MarketPriceHistory productName={ProductName}", productName);
            return Array.Empty<MarketPriceHistory>();
        }
    }
}