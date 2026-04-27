using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Catalog;
using Simcag.MarketDataService.Application.Classification;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Application.Ports;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Domain.ValueObjects;

namespace Simcag.MarketDataService.Application.Services;

public class MarketDataService : IMarketDataService
{
    private readonly ILogger<MarketDataService> _logger;
    private readonly IMarketDataCacheService _cacheService;
    private readonly IMarketPriceRepository _repository;
    private readonly IMockMarketProductCatalog _mockCatalog;
    private readonly IRuleBasedExpenseCategoryClassifier _classifier;
    private readonly IMarketDataUpdatedEventPublisher _eventPublisher;
    private readonly Random _random = new();

    public MarketDataService(
        ILogger<MarketDataService> logger,
        IMarketDataCacheService cacheService,
        IMarketPriceRepository repository,
        IMockMarketProductCatalog mockCatalog,
        IRuleBasedExpenseCategoryClassifier classifier,
        IMarketDataUpdatedEventPublisher eventPublisher)
    {
        _logger = logger;
        _cacheService = cacheService;
        _repository = repository;
        _mockCatalog = mockCatalog;
        _classifier = classifier;
        _eventPublisher = eventPublisher;
    }

    public async Task<MarketPrice?> GetPriceAsync(string productName, CancellationToken ct)
    {
        var cachedPrice = await _cacheService.GetMarketPriceAsync(productName, ct);
        if (cachedPrice != null)
            return cachedPrice;

        var standardizedName = ProductNameNormalizer.Normalize(productName);
        var searchNames = new[] { productName, standardizedName }.Distinct().ToArray();

        foreach (var searchName in searchNames)
        {
            var fromDb = await _repository.GetByProductNameAsync(searchName, ct);
            if (fromDb != null)
            {
                var adjusted = ApplyCategoryAdjustment(
                    fromDb.Price,
                    _classifier.Classify(fromDb.ProductName));
                var result = MarketPrice.Create(fromDb.ProductName, Math.Round(adjusted, 2), "PostgreSQL");
                await _cacheService.SetMarketPriceAsync(result, ct);
                return result;
            }
        }

        MarketPrice? foundPrice = null;
        string matchedName = productName;

        foreach (var searchName in searchNames)
        {
            foundPrice = await FindProductPriceAsync(searchName, ct);
            if (foundPrice != null)
            {
                matchedName = searchName;
                break;
            }
        }

        if (foundPrice == null)
        {
            _logger.LogWarning("No market price found for product: {ProductName} (tried variations)", productName);
            return null;
        }

        var category = _classifier.Classify(matchedName);
        var adjustedPrice = ApplyCategoryAdjustment(foundPrice.Price, category);
        var marketPrice = MarketPrice.Create(productName, Math.Round(adjustedPrice, 2), "RuleBasedMarketData");

        await _cacheService.SetMarketPriceAsync(marketPrice, ct);

        _logger.LogInformation(
            "Resolved market price for {ProductName} (matched: {MatchedName}, category: {Category}): {Price:C}",
            productName, matchedName, category, adjustedPrice);

        return marketPrice;
    }

    private async Task<MarketPrice?> FindProductPriceAsync(string searchName, CancellationToken ct)
    {
        await _mockCatalog.EnsureSeededAsync(ct);

        var found = _mockCatalog.GetBasePrices().FirstOrDefault(kvp =>
            kvp.Key.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
            searchName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
            CalculateSimilarity(kvp.Key, searchName) > 0.7);

        if (found.Key == null)
            return null;

        var originalPrice = found.Value;
        var variation = (decimal)(_random.NextDouble() * 0.1 - 0.05);
        var currentPrice = originalPrice * (1 + variation);

        return MarketPrice.Create(searchName, Math.Round(currentPrice, 2), "MockMarketData");
    }

    private static decimal ApplyCategoryAdjustment(decimal basePrice, string category) =>
        category switch
        {
            "Notebook" => basePrice * 1.1m,
            "Hardware" => basePrice * 1.05m,
            "Monitor" => basePrice * 1.02m,
            _ => basePrice
        };

    private static double CalculateSimilarity(string str1, string str2)
    {
        if (string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        var longer = str1.Length > str2.Length ? str1 : str2;
        var shorter = str1.Length > str2.Length ? str2 : str1;

        if (longer.Contains(shorter, StringComparison.OrdinalIgnoreCase))
            return (double)shorter.Length / longer.Length;

        return 0.0;
    }

    public async Task<IEnumerable<MarketPriceHistory>> GetPriceHistoryAsync(string productName, int days, CancellationToken ct)
    {
        await _mockCatalog.EnsureSeededAsync(ct);

        var history = new List<MarketPriceHistory>();
        var basePrice = 100m;

        var existingPrice = _mockCatalog.GetBasePrices().FirstOrDefault(kvp =>
            kvp.Key.Contains(productName, StringComparison.OrdinalIgnoreCase) ||
            productName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

        if (existingPrice.Key != null)
            basePrice = existingPrice.Value;

        for (var i = days; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i);
            var variation = (decimal)(_random.NextDouble() * 0.2 - 0.1);
            var price = basePrice * (1 + variation);

            history.Add(MarketPriceHistory.Create(
                productName,
                Math.Round(price, 2),
                "MockMarketData",
                date));
        }

        _logger.LogInformation("Generated {Count} days of price history for {ProductName}", history.Count, productName);

        return history;
    }

    public async Task SeedMockDataAsync(CancellationToken ct)
    {
        await _mockCatalog.EnsureSeededAsync(ct);

        var distinctCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (productName, price) in _mockCatalog.GetBasePrices())
        {
            var category = _classifier.Classify(productName);
            distinctCategories.Add(category);

            var existing = await _repository.GetByProductNameAsync(productName, ct);
            if (existing != null)
                continue;

            var observation = MarketPrice.CreateObservation(
                productName,
                price,
                "MockData",
                category,
                GeographicRegion.DefaultSeedRegion);

            await _repository.AddAsync(observation, ct);
        }

        foreach (var category in distinctCategories)
        {
            await _eventPublisher.PublishAsync(
                new MarketDataUpdatedEvent(category, GeographicRegion.DefaultSeedRegion, DateTime.UtcNow),
                ct);
        }

        _logger.LogInformation("Seeded mock catalog and persisted {Count} categories to PostgreSQL (where missing)", distinctCategories.Count);
    }
}
