using Simcag.MarketDataService.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Interfaces;

public interface IMarketPriceRepository
{
    Task<MarketPrice?> GetByProductNameAsync(string productName, CancellationToken ct);
    Task AddAsync(MarketPrice marketPrice, CancellationToken ct);
    Task UpdateAsync(MarketPrice marketPrice, CancellationToken ct);
    Task<IEnumerable<MarketPrice>> GetAllAsync(CancellationToken ct);

    Task<IReadOnlyList<MarketPrice>> GetActiveByCategoryAndRegionAsync(
        string expenseCategory,
        string geographicRegion,
        CancellationToken ct);
}

public interface IMarketPriceHistoryRepository
{
    Task AddAsync(MarketPriceHistory history, CancellationToken ct);
    Task<IEnumerable<MarketPriceHistory>> GetByProductNameAsync(string productName, int days, CancellationToken ct);
}
