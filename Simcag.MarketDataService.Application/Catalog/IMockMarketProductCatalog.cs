namespace Simcag.MarketDataService.Application.Catalog;

/// <summary>In-memory seed catalog for local/dev benchmarking when DB has no rows.</summary>
public interface IMockMarketProductCatalog
{
    Task EnsureSeededAsync(CancellationToken ct);
    IReadOnlyDictionary<string, decimal> GetBasePrices();
}
