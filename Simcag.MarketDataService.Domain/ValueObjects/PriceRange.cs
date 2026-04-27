namespace Simcag.MarketDataService.Domain.ValueObjects;

public readonly record struct PriceRange(decimal Min, decimal Max)
{
    public static PriceRange FromSamples(IReadOnlyList<decimal> samples)
    {
        if (samples.Count == 0)
            return new PriceRange(0, 0);
        return new PriceRange(samples.Min(), samples.Max());
    }
}
