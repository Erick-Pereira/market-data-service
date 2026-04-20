using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Services;

public class MarketDataService : IMarketDataService
{
    private readonly ILogger<MarketDataService> _logger;
    private readonly IMarketDataCacheService _cacheService;
    private readonly Dictionary<string, MarketPrice> _mockPrices;
    private readonly Random _random = new();

    public MarketDataService(
        ILogger<MarketDataService> logger,
        IMarketDataCacheService cacheService)
    {
        _logger = logger;
        _cacheService = cacheService;
        _mockPrices = new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<MarketPrice?> GetPriceAsync(string productName, CancellationToken ct)
    {
        // Check cache first
        var cachedPrice = await _cacheService.GetMarketPriceAsync(productName, ct);
        if (cachedPrice != null)
        {
            return cachedPrice;
        }

        // Try AI-enhanced product name standardization for better matching
        var standardizedName = await StandardizeProductNameWithAIAsync(productName, ct);
        var searchNames = new[] { productName, standardizedName };

        // Search for product using multiple name variations
        MarketPrice? foundPrice = null;
        string matchedName = productName;

        foreach (var searchName in searchNames.Distinct())
        {
            foundPrice = await FindProductPriceAsync(searchName, ct);
            if (foundPrice != null)
            {
                matchedName = searchName;
                break;
            }
        }

        if (foundPrice != null)
        {
            // Apply AI-enhanced categorization for better market data
            var category = await CategorizeProductWithAIAsync(matchedName, ct);

            // Adjust price based on category (premium categories might have higher prices)
            var adjustedPrice = ApplyCategoryAdjustment(foundPrice.Price, category);

            var marketPrice = MarketPrice.Create(productName, Math.Round(adjustedPrice, 2), "AIEnhancedMarketData");

            // Cache the result with metadata
            await _cacheService.SetMarketPriceAsync(marketPrice, ct);

            _logger.LogInformation("AI-enhanced market price for {ProductName} (matched: {MatchedName}, category: {Category}): {Price:C}",
                productName, matchedName, category, adjustedPrice);

            return marketPrice;
        }

        _logger.LogWarning("No market price found for product: {ProductName} (tried variations)", productName);
        return null;
    }

    private async Task<MarketPrice?> FindProductPriceAsync(string searchName, CancellationToken ct)
    {
        // Cache miss - generate mock data
        if (_mockPrices.Count == 0)
        {
            await SeedMockDataAsync(ct);
        }

        // Try to find the product (case-insensitive with fuzzy matching)
        var found = _mockPrices.FirstOrDefault(kvp =>
            kvp.Key.Contains(searchName, StringComparison.OrdinalIgnoreCase) ||
            searchName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
            CalculateSimilarity(kvp.Key, searchName) > 0.7); // 70% similarity threshold

        if (found.Key != null)
        {
            // Add some random variation (±5%) to simulate market fluctuation
            var originalPrice = found.Value.Price;
            var variation = (decimal)(_random.NextDouble() * 0.1 - 0.05); // -5% to +5%
            var currentPrice = originalPrice * (1 + variation);

            return MarketPrice.Create(searchName, Math.Round(currentPrice, 2), "MockMarketData");
        }

        return null;
    }

    private async Task<string> StandardizeProductNameWithAIAsync(string productName, CancellationToken ct)
    {
        try
        {
            // In a real implementation, this would call the AI Service
            // For now, use simple standardization
            return productName
                .Trim()
                .Replace("  ", " ")
                .Split(' ')
                .Select(word => word.Length > 0 ?
                    char.ToUpper(word[0]) + word[1..].ToLower() :
                    word)
                .Aggregate((current, next) => current + " " + next);
        }
        catch
        {
            return productName; // Return original if AI fails
        }
    }

    private async Task<string> CategorizeProductWithAIAsync(string productName, CancellationToken ct)
    {
        try
        {
            // In a real implementation, this would call the AI Service
            // For now, use simple rule-based categorization
            var name = productName.ToLower();

            if (name.Contains("notebook") || name.Contains("laptop"))
                return "Notebook";
            if (name.Contains("monitor") || name.Contains("display"))
                return "Monitor";
            if (name.Contains("mouse") || name.Contains("keyboard"))
                return "Periférico";
            if (name.Contains("ram") || name.Contains("ssd") || name.Contains("cpu"))
                return "Hardware";

            return "Outro";
        }
        catch
        {
            return "Outro"; // Default category if AI fails
        }
    }

    private decimal ApplyCategoryAdjustment(decimal basePrice, string category)
    {
        // Apply category-based price adjustments (premium categories = higher prices)
        var adjustment = category switch
        {
            "Notebook" => 1.1m,    // 10% premium
            "Hardware" => 1.05m,   // 5% premium
            "Monitor" => 1.02m,    // 2% premium
            _ => 1.0m             // No adjustment
        };

        return basePrice * adjustment;
    }

    private double CalculateSimilarity(string str1, string str2)
    {
        // Simple Levenshtein distance-based similarity
        // In a real implementation, you'd use a proper string similarity algorithm
        if (str1 == str2) return 1.0;

        var longer = str1.Length > str2.Length ? str1 : str2;
        var shorter = str1.Length > str2.Length ? str2 : str1;

        if (longer.Contains(shorter, StringComparison.OrdinalIgnoreCase))
            return (double)shorter.Length / longer.Length;

        return 0.0; // No similarity
    }

    public async Task<IEnumerable<MarketPriceHistory>> GetPriceHistoryAsync(string productName, int days, CancellationToken ct)
    {
        // Check if we have mock data loaded
        if (_mockPrices.Count == 0)
        {
            await SeedMockDataAsync(ct);
        }

        var history = new List<MarketPriceHistory>();
        var basePrice = 100m; // Default base price

        // Try to find existing price
        var existingPrice = _mockPrices.FirstOrDefault(kvp =>
            kvp.Key.Contains(productName, StringComparison.OrdinalIgnoreCase) ||
            productName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));

        if (existingPrice.Key != null)
        {
            basePrice = existingPrice.Value.Price;
        }

        // Generate historical data with ±10% variation
        for (int i = days; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i);
            var variation = (decimal)(_random.NextDouble() * 0.2 - 0.1); // -10% to +10%
            var price = basePrice * (1 + variation);

            history.Add(MarketPriceHistory.Create(
                productName,
                Math.Round(price, 2),
                "MockMarketData",
                date));
        }

        _logger.LogInformation("Generated {Count} days of price history for {ProductName}",
            history.Count, productName);

        return history;
    }

    public async Task SeedMockDataAsync(CancellationToken ct)
    {
        if (_mockPrices.Count > 0)
        {
            _logger.LogInformation("Mock data already seeded");
            return;
        }

        var mockData = new Dictionary<string, decimal>
        {
            ["Notebook Dell Inspiron 15"] = 4200.00m,
            ["Monitor LG 24\""] = 899.00m,
            ["Teclado Mecânico RGB"] = 350.00m,
            ["Mouse Gamer Logitech"] = 150.00m,
            ["SSD 1TB NVMe"] = 450.00m,
            ["Placa de Vídeo RTX 3060"] = 2800.00m,
            ["Processador Intel i7"] = 1800.00m,
            ["Memória RAM 16GB DDR4"] = 320.00m,
            ["Fonte 650W 80 Plus"] = 280.00m,
            ["Gabinete Gamer"] = 220.00m
        };

        foreach (var (productName, price) in mockData)
        {
            _mockPrices[productName] = MarketPrice.Create(productName, price, "MockData");
        }

        _logger.LogInformation("Seeded {Count} mock market prices", _mockPrices.Count);
    }
}