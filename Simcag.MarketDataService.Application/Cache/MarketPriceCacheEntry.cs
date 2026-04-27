namespace Simcag.MarketDataService.Application.Cache;

/// <summary>Redis-serializable shape for single-product cache entries (avoids EF entity JSON issues).</summary>
public sealed class MarketPriceCacheEntry
{
    public string ProductName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Source { get; init; } = string.Empty;
    public DateTime CollectedDate { get; init; }
}
