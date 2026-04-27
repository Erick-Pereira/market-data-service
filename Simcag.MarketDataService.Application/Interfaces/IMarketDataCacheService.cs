using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Interfaces;

public interface IMarketDataCacheService
{
    Task<MarketPrice?> GetMarketPriceAsync(string productName, CancellationToken ct);
    Task SetMarketPriceAsync(MarketPrice marketPrice, CancellationToken ct);
    Task RemoveMarketPriceAsync(string productName, CancellationToken ct);
    Task ClearAllCacheAsync(CancellationToken ct);

    Task<MarketDataResponseDto?> GetBenchmarkAsync(string category, string region, CancellationToken ct);
    Task SetBenchmarkAsync(MarketDataResponseDto dto, CancellationToken ct);
}