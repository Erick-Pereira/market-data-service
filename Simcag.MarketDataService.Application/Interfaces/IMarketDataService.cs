using Simcag.MarketDataService.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Interfaces;

public interface IMarketDataService
{
    Task<MarketPrice?> GetPriceAsync(string productName, CancellationToken ct);
    Task<IEnumerable<MarketPriceHistory>> GetPriceHistoryAsync(string productName, int days, CancellationToken ct);
    Task SeedMockDataAsync(CancellationToken ct);
}