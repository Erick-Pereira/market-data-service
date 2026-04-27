namespace Simcag.MarketDataService.Domain.ValueObjects;

/// <summary>Geographic region key (e.g. state/metro code) for market segmentation.</summary>
public readonly record struct GeographicRegion(string Value)
{
    public const string DefaultSeedRegion = "BR-Nacional";

    public string Normalized => string.IsNullOrWhiteSpace(Value)
        ? string.Empty
        : Value.Trim();

    public static GeographicRegion FromInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new GeographicRegion(string.Empty);
        var v = input.Trim();
        if (v.Length > 64)
            v = v[..64];
        return new GeographicRegion(v);
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Normalized);
}
