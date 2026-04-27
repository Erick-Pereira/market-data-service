using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;

namespace Simcag.MarketDataService.Application.Cache;

/// <summary>Usado quando Redis não está disponível: API continua a responder sem cache.</summary>
public sealed class NoOpMarketDataCacheService : IMarketDataCacheService
{
    public Task<MarketDataResponseDto?> GetBenchmarkAsync(string category, string region, CancellationToken ct) =>
        Task.FromResult<MarketDataResponseDto?>(null);

    public Task<MarketPrice?> GetMarketPriceAsync(string productName, CancellationToken ct) =>
        Task.FromResult<MarketPrice?>(null);

    public Task SetMarketPriceAsync(MarketPrice marketPrice, CancellationToken ct) => Task.CompletedTask;
    public Task RemoveMarketPriceAsync(string productName, CancellationToken ct) => Task.CompletedTask;
    public Task ClearAllCacheAsync(CancellationToken ct) => Task.CompletedTask;
    public Task SetBenchmarkAsync(MarketDataResponseDto dto, CancellationToken ct) => Task.CompletedTask;
}
