using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.Shared.Contracts;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Mediana de observações reais em <see cref="MarketPriceHistory"/> (sem catálogo curado).
/// </summary>
public static class HistoricalPriceBenchmarkResolver
{
    public const string SourcePrefix = "PostgreSQL:PriceHistory";

    private const int DefaultHistoryDays = 180;

    public static async Task<MarketPriceResearchResult?> TryResolveAsync(
        IMarketPriceHistoryRepository historyRepository,
        IReadOnlyList<string> searchNames,
        decimal? declaredReferenceBrl,
        CancellationToken ct,
        int historyDays = DefaultHistoryDays)
    {
        var points = new List<MarketPriceHistory>();
        foreach (var name in searchNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var rows = await historyRepository.GetByProductNameAsync(name.Trim(), historyDays, ct);
            points.AddRange(rows.Where(h => !IsLegacyCuratedSource(h.Source)));
        }

        if (points.Count == 0)
            return null;

        var prices = points
            .Select(h => h.Price)
            .Where(p => p > 0.01m)
            .ToList();

        if (declaredReferenceBrl is > 0.01m)
            prices = DeclaredReferencePlausibility.FilterSamples(prices, declaredReferenceBrl.Value).ToList();

        if (prices.Count == 0)
            return null;

        prices.Sort();
        var median = prices[prices.Count / 2];
        var spread = prices.Count >= 2
            ? (prices[^1] - prices[0]) / Math.Max(median, 0.01m)
            : 0m;

        var latest = points.Max(h => h.CollectedDate);
        var evidence =
            $"Mediana de {prices.Count} observação(ões) históricas em PostgreSQL (última: {latest:yyyy-MM-dd}).";

        return new MarketPriceResearchResult(
            Math.Round(median, 2),
            SourcePrefix,
            evidence,
            prices.Count,
            spread,
            null,
            BenchmarkPriceKind.ExternalMarketEstimate,
            BenchmarkStatuses.DatabaseHit,
            ConfidenceScore: Math.Clamp(0.35m + prices.Count * 0.05m, 0.35m, 0.75m),
            BenchmarkQualityScore: 0.55m,
            new[]
            {
                new BenchmarkDiagnosticEntry(
                    "price_history_median",
                    SourcePrefix,
                    evidence,
                    median.ToString(System.Globalization.CultureInfo.InvariantCulture))
            },
            points
                .OrderByDescending(h => h.CollectedDate)
                .Take(5)
                .Select(h => new MarketPriceSample
                {
                    Label = h.ProductName,
                    Url = string.Empty,
                    PriceBrl = h.Price,
                    Provider = "history",
                })
                .ToList());
    }

    private static bool IsLegacyCuratedSource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && source.Contains("CuratedCategoryBenchmark", StringComparison.OrdinalIgnoreCase);
}
