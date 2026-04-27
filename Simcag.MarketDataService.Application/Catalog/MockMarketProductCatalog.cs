using Microsoft.Extensions.Logging;

namespace Simcag.MarketDataService.Application.Catalog;

public sealed class MockMarketProductCatalog : IMockMarketProductCatalog
{
    private readonly ILogger<MockMarketProductCatalog> _logger;
    private readonly Dictionary<string, decimal> _prices = new(StringComparer.OrdinalIgnoreCase);

    public MockMarketProductCatalog(ILogger<MockMarketProductCatalog> logger) => _logger = logger;

    public IReadOnlyDictionary<string, decimal> GetBasePrices()
    {
        lock (_prices)
            return new Dictionary<string, decimal>(_prices, StringComparer.OrdinalIgnoreCase);
    }

    public Task EnsureSeededAsync(CancellationToken ct)
    {
        lock (_prices)
        {
            if (_prices.Count > 0)
            {
                _logger.LogDebug("Mock catalog already seeded");
                return Task.CompletedTask;
            }

            foreach (var (name, price) in SeedRows)
                _prices[name] = price;

            _logger.LogInformation("Seeded {Count} mock catalog products", _prices.Count);
        }

        return Task.CompletedTask;
    }

    private static readonly IReadOnlyList<KeyValuePair<string, decimal>> SeedRows =
    [
        new("Notebook Dell Inspiron 15", 4200.00m),
        new("Monitor LG 24\"", 899.00m),
        new("Teclado Mecânico RGB", 350.00m),
        new("Mouse Gamer Logitech", 150.00m),
        new("SSD 1TB NVMe", 450.00m),
        new("Placa de Vídeo RTX 3060", 2800.00m),
        new("Processador Intel i7", 1800.00m),
        new("Memória RAM 16GB DDR4", 320.00m),
        new("Fonte 650W 80 Plus", 280.00m),
        new("Gabinete Gamer", 220.00m)
    ];
}
