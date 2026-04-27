namespace Simcag.MarketDataService.Domain.Aggregation;

/// <summary>Aggregated market reference for a category × region (no persistence coupling).</summary>
public sealed record MarketBenchmarkSnapshot(
    string Category,
    string Region,
    decimal AveragePrice,
    decimal MedianPrice,
    decimal MinPrice,
    decimal MaxPrice,
    int SampleSize,
    DateTime LastUpdatedUtc);
