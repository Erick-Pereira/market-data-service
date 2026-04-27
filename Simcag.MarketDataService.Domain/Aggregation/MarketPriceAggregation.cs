using Simcag.MarketDataService.Domain.ValueObjects;

namespace Simcag.MarketDataService.Domain.Aggregation;

/// <summary>
/// Pure domain rules: IQR outlier trimming, mean/median, and benchmark snapshot construction.
/// </summary>
public static class MarketPriceAggregation
{
    /// <summary>Removes values outside [Q1 - k*IQR, Q3 + k*IQR]. If fewer than 4 samples, returns a copy of input.</summary>
    public static IReadOnlyList<decimal> RemoveOutliersIqr(IReadOnlyList<decimal> prices, decimal k = 1.5m)
    {
        if (prices.Count < 4)
            return prices.ToArray();

        var sorted = prices.OrderBy(p => p).ToArray();
        var q1 = Quantile(sorted, 0.25m);
        var q3 = Quantile(sorted, 0.75m);
        var iqr = q3 - q1;
        if (iqr <= 0)
            return prices.ToArray();

        var low = q1 - k * iqr;
        var high = q3 + k * iqr;
        return sorted.Where(p => p >= low && p <= high).ToArray();
    }

    public static decimal Median(IReadOnlyList<decimal> sortedOrAny)
    {
        if (sortedOrAny.Count == 0)
            return 0;
        var s = sortedOrAny.OrderBy(x => x).ToArray();
        var mid = s.Length / 2;
        return s.Length % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2m;
    }

    public static decimal Mean(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
            return 0;
        return values.Sum() / values.Count;
    }

    public static MarketBenchmarkSnapshot BuildSnapshot(
        ExpenseCategory category,
        GeographicRegion region,
        IReadOnlyList<decimal> rawPrices,
        DateTime? lastUpdatedUtc = null)
    {
        if (rawPrices.Count == 0)
        {
            return new MarketBenchmarkSnapshot(
                category.Normalized,
                region.Normalized,
                0, 0, 0, 0,
                0,
                lastUpdatedUtc ?? DateTime.UtcNow);
        }

        var trimmed = RemoveOutliersIqr(rawPrices);
        if (trimmed.Count == 0)
            trimmed = rawPrices.ToArray();

        var range = PriceRange.FromSamples(trimmed);
        return new MarketBenchmarkSnapshot(
            category.Normalized,
            region.Normalized,
            Mean(trimmed),
            Median(trimmed),
            range.Min,
            range.Max,
            trimmed.Count,
            lastUpdatedUtc ?? DateTime.UtcNow);
    }

    private static decimal Quantile(IReadOnlyList<decimal> sorted, decimal p)
    {
        if (sorted.Count == 0)
            return 0;
        var pos = (sorted.Count - 1) * p;
        var lo = (int)Math.Floor((double)pos);
        var hi = (int)Math.Ceiling((double)pos);
        if (lo == hi)
            return sorted[lo];
        var w = pos - lo;
        return sorted[lo] * (1 - w) + sorted[hi] * w;
    }
}
