using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Catalog;
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
    private readonly IMockMarketProductCatalog _mockCatalog;
    private readonly IRuleBasedExpenseCategoryClassifier _classifier;
    private readonly ILogger<MarketBenchmarkQueryService> _logger;

    public MarketBenchmarkQueryService(
        IMarketPriceRepository repository,
        IMarketDataCacheService cache,
        IMockMarketProductCatalog mockCatalog,
        IRuleBasedExpenseCategoryClassifier classifier,
        ILogger<MarketBenchmarkQueryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _mockCatalog = mockCatalog;
        _classifier = classifier;
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

        if (decimals.Count == 0)
        {
            await _mockCatalog.EnsureSeededAsync(ct);
            decimals = BuildFallbackPricesFromMockCatalog(cat, reg);
            lastUpdated ??= DateTime.UtcNow;
        }

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
            "Computed market benchmark for {Category}/{Region}: n={Sample}, avg={Avg}",
            dto.Category, dto.Region, dto.SampleSize, dto.AveragePrice);

        return dto;
    }

    private List<decimal> BuildFallbackPricesFromMockCatalog(ExpenseCategory cat, GeographicRegion reg)
    {
        // Mock catalog is only keyed by product; region filter uses default seed region or any match.
        var prices = new List<decimal>();
        foreach (var (product, basePrice) in _mockCatalog.GetBasePrices())
        {
            var productCategory = _classifier.Classify(product);
            if (!string.Equals(productCategory, cat.Normalized, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(reg.Normalized, GeographicRegion.DefaultSeedRegion, StringComparison.OrdinalIgnoreCase))
                continue;

            prices.Add(ApplyCategoryMultiplier(basePrice, productCategory));
        }

        return prices;
    }

    private static decimal ApplyCategoryMultiplier(decimal basePrice, string category) =>
        category switch
        {
            "Notebook" => basePrice * 1.1m,
            "Hardware" => basePrice * 1.05m,
            "Monitor" => basePrice * 1.02m,
            _ => basePrice
        };
}
