namespace Simcag.MarketDataService.Domain.Entities;

/// <summary>
/// Catálogo de produtos cadastrados no sistema.
/// Usado para benchmark periódico (não scraping em tempo real).
/// </summary>
public class ProductCatalogEntry
{
    public Guid Id { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public decimal? LastBenchmarkPrice { get; private set; }
    public DateTime LastBenchmarkDate { get; private set; }
    public bool IsActive { get; private set; }

    private ProductCatalogEntry() { }

    public static ProductCatalogEntry Create(
        string productName, 
        string category, 
        decimal? lastBenchmarkPrice = null)
    {
        return new ProductCatalogEntry
        {
            Id = Guid.NewGuid(),
            ProductName = productName.Trim(),
            NormalizedName = ProductNameNormalizer.Normalize(productName).Trim(),
            Category = category.Trim(),
            LastBenchmarkPrice = lastBenchmarkPrice,
            LastBenchmarkDate = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void UpdateBenchmark(decimal newPrice)
    {
        LastBenchmarkPrice = newPrice;
        LastBenchmarkDate = DateTime.UtcNow;
    }
}
