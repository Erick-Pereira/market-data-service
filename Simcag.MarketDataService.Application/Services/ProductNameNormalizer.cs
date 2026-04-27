namespace Simcag.MarketDataService.Application.Services;

/// <summary>Deterministic normalization for search keys (no external services).</summary>
public static class ProductNameNormalizer
{
    public static string Normalize(string productName)
    {
        try
        {
            return productName
                .Trim()
                .Replace("  ", " ", StringComparison.Ordinal)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length > 0 ? char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant() : word)
                .Aggregate((current, next) => current + " " + next);
        }
        catch
        {
            return productName;
        }
    }
}
