using Simcag.MarketDataService.Domain.Entities;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Decide se um preço em PostgreSQL ainda deve ser usado ou se a pesquisa web deve ser tentada.
/// </summary>
public static class StoredMarketPricePolicy
{
    public const decimal MaxDeclaredVsStoredRelativeDelta = 0.40m;

    public static bool ShouldRefresh(decimal? declaredReferenceBrl, MarketPrice stored)
    {
        if (IsDocumentAnchorSource(stored.Source))
            return true;

        if (declaredReferenceBrl is not > 0.01m)
            return false;

        var storedPrice = stored.Price;
        if (storedPrice <= 0.01m)
            return true;

        var delta = Math.Abs(declaredReferenceBrl.Value - storedPrice);
        var basePrice = Math.Min(declaredReferenceBrl.Value, storedPrice);
        if (basePrice <= 0.01m)
            return true;

        return delta / basePrice > MaxDeclaredVsStoredRelativeDelta;
    }

    public static bool IsDocumentAnchorSource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && source.Contains("DocumentDeclaredReference", StringComparison.OrdinalIgnoreCase);

    public static bool IsExternalBenchmarkSource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && !IsDocumentAnchorSource(source);

    public static bool IsCuratedCategorySource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && source.Contains("CuratedCategoryBenchmark", StringComparison.OrdinalIgnoreCase);
}
