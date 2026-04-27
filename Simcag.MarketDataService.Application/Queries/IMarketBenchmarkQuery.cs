using Simcag.MarketDataService.Application.DTOs;

namespace Simcag.MarketDataService.Application.Queries;

public interface IMarketBenchmarkQuery
{
    Task<MarketDataResponseDto> GetAsync(string category, string region, CancellationToken ct);
}
