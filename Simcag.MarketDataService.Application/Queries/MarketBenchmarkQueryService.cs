using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Classification;
using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Aggregation;
using Simcag.MarketDataService.Domain.ValueObjects;

namespace Simcag.MarketDataService.Application.Queries;

public sealed class MarketBenchmarkQueryService : IMarketBenchmarkQuery
{
    private readonly IMarketPriceRepository _repository;
    private readonly IMarketDataCacheService _cache;
    private readonly ILogger<MarketBenchmarkQueryService> _logger;

    public MarketBenchmarkQueryService(
        IMarketPriceRepository repository,
        IMarketDataCacheService cache,
        ILogger<MarketBenchmarkQueryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MarketDataResponseDto> GetAsync(string category, string region, CancellationToken ct)
    {
        var cat = ExpenseCategory.FromInput(category);
        var reg = GeographicRegion.FromInput(region);
        if (cat.IsEmpty || reg.IsEmpty)
            throw new ArgumentException("category and region are required.");

        var cached = await _cache.GetBenchmarkAsync(cat.Normalized, reg.Normalized, ct);
        if (cached != null)
            return cached;

        var rows = await _repository.GetActiveByCategoryAndRegionAsync(cat.Normalized, reg.Normalized, ct);
        var decimals = rows.Select(p => p.Price).ToList();
        DateTime? lastUpdated = rows.Count > 0 ? rows.Max(r => r.CollectedDate) : null;

        var snapshot = MarketPriceAggregation.BuildSnapshot(cat, reg, decimals, lastUpdated);
        var dto = new MarketDataResponseDto
        {
            Category = snapshot.Category,
            Region = snapshot.Region,
            AveragePrice = Math.Round(snapshot.AveragePrice, 2),
            MedianPrice = Math.Round(snapshot.MedianPrice, 2),
            MinPrice = Math.Round(snapshot.MinPrice, 2),
            MaxPrice = Math.Round(snapshot.MaxPrice, 2),
            SampleSize = snapshot.SampleSize,
            LastUpdated = snapshot.LastUpdatedUtc
        };

        await _cache.SetBenchmarkAsync(dto, ct);
        _logger.LogInformation(
            "Benchmark {Category}/{Region}: n={Sample}, avg={Avg}",
            dto.Category, dto.Region, dto.SampleSize, dto.AveragePrice);

        return dto;
    }
}
