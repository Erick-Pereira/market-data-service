using System.Linq;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Classification;
using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Domain.ValueObjects;

namespace Simcag.MarketDataService.Application.Services;

internal static class MarketPriceSources
{
    public const string DocumentDeclaredReference = "DocumentDeclaredReference";
}

/// <summary>
/// Preços de mercado a partir de (1) PostgreSQL, (2) pesquisa web via <see cref="IMarketPriceResearchService"/>,
/// ou (3) valor declarado na linha (<paramref name="declaredReferenceBrl"/>) quando a web não devolve BRL — persiste como referência auditável.
/// </summary>
public class MarketDataService : IMarketDataService
{
    /// <summary>Matches EF <c>Source</c> column max length (100) on market-data tables.</summary>
    private const int MaxPersistedSourceLength = 100;

    private readonly ILogger<MarketDataService> _logger;
    private readonly IMarketDataCacheService _cacheService;
    private readonly IMarketPriceRepository _repository;
    private readonly IMarketPriceHistoryRepository _historyRepository;
    private readonly IMarketPriceResearchService _research;
    private readonly IRuleBasedExpenseCategoryClassifier _classifier;

    public MarketDataService(
        ILogger<MarketDataService> logger,
        IMarketDataCacheService cacheService,
        IMarketPriceRepository repository,
        IMarketPriceHistoryRepository historyRepository,
        IMarketPriceResearchService research,
        IRuleBasedExpenseCategoryClassifier classifier)
    {
        _logger = logger;
        _cacheService = cacheService;
        _repository = repository;
        _historyRepository = historyRepository;
        _research = research;
        _classifier = classifier;
    }

    private static string ClampPersistedSource(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= MaxPersistedSourceLength ? text : text[..MaxPersistedSourceLength];
    }

    public async Task<MarketPrice?> GetPriceAsync(string productName, CancellationToken ct, decimal? declaredReferenceBrl = null)
    {
        var resolution = await ResolvePriceAsync(productName, ct, declaredReferenceBrl);
        return resolution?.Quote;
    }

    public async Task<MarketPriceResolution?> ResolvePriceAsync(
        string productName,
        CancellationToken ct,
        decimal? declaredReferenceBrl = null)
    {
        var trimmed = productName.Trim();
        var normalizedLabel = ProductNameNormalizer.Normalize(trimmed);

        var cachedPrice = await _cacheService.GetMarketPriceAsync(trimmed, ct);
        if (cachedPrice != null)
        {
            return new MarketPriceResolution(
                cachedPrice,
                null,
                null,
                null,
                normalizedLabel,
                "redis-cache",
                BenchmarkPriceKind.ExternalMarketEstimate,
                BenchmarkStatuses.CacheHit);
        }

        var standardizedName = normalizedLabel;
        var searchNames = new[] { trimmed, standardizedName }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var searchName in searchNames)
        {
            var fromDb = await _repository.GetByProductNameAsync(searchName, ct);
            if (fromDb is not null)
            {
                var result = MarketPrice.Create(fromDb.ProductName, Math.Round(fromDb.Price, 2), "PostgreSQL");
                await _cacheService.SetMarketPriceAsync(result, ct);
                return new MarketPriceResolution(
                    result,
                    null,
                    null,
                    null,
                    normalizedLabel,
                    "postgresql",
                    BenchmarkPriceKind.ExternalMarketEstimate,
                    BenchmarkStatuses.DatabaseHit);
            }
        }

        MarketPriceResearchDetailedOutcome? lastWeb = null;
        MarketPriceResearchResult? researched = null;
        foreach (var query in searchNames)
        {
            lastWeb = await _research.TryResolvePriceDetailedAsync(query, ct);
            if (lastWeb.Result is not null)
            {
                researched = lastWeb.Result;
                break;
            }
        }

        if (researched is null
            && declaredReferenceBrl is > 0.01m and <= 50_000_000m)
        {
            var rounded = Math.Round(declaredReferenceBrl.Value, 2);
            researched = new MarketPriceResearchResult(
                rounded,
                MarketPriceSources.DocumentDeclaredReference,
                "Valor declarado na linha do documento (referência quando a pesquisa web não devolve cotação em BRL).",
                1,
                0,
                null,
                BenchmarkPriceKind.DocumentAnchorPrice,
                BenchmarkStatuses.DocumentAnchor,
                0.22m,
                0.12m,
                new[]
                {
                    new BenchmarkDiagnosticEntry(
                        "document_anchor_price",
                        "declared_line",
                        "Valor da linha usado como âncora (não é external_market_estimate).",
                        rounded.ToString(System.Globalization.CultureInfo.InvariantCulture))
                });
            _logger.LogInformation(
                "Âncora documental (document_anchor_price) para {ProductName}: {Price}; rejeições web prévias={Rejections}",
                productName,
                rounded,
                lastWeb is null ? "" : string.Join(";", lastWeb.RejectionReasons));
        }

        if (researched is null)
        {
            _logger.LogInformation(
                "Sem preço de mercado real para: {ProductName} (base vazia e pesquisa sem resultado).",
                productName);
            return null;
        }

        var quote = MarketPrice.Create(trimmed, Math.Round(researched.Price, 2), researched.Source);

        if (researched.BenchmarkKind != BenchmarkPriceKind.DocumentAnchorPrice)
            await _cacheService.SetMarketPriceAsync(quote, ct);

        await PersistMarketObservationAsync(quote, researched.EvidenceSnippet, ct);

        _logger.LogInformation(
            "Preço obtido ({Source}) para {ProductName}: {Price:C} (amostras={Samples}, spread={Spread:F3}, confiança={Confidence})",
            researched.Source,
            productName,
            researched.Price,
            researched.SampleCount,
            researched.RelativeSpread,
            ClassifyConfidence(researched));

        var sample = researched.SampleCount > 0 ? researched.SampleCount : (int?)null;
        var spread = researched.RelativeSpread > 0 ? researched.RelativeSpread : (decimal?)null;

        return new MarketPriceResolution(
            quote,
            sample,
            spread,
            researched.SearchQueryUsed,
            normalizedLabel,
            ClassifyConfidence(researched),
            researched.BenchmarkKind,
            researched.BenchmarkStatus,
            researched.ConfidenceScore,
            researched.BenchmarkQualityScore,
            researched.Diagnostics,
            researched.BenchmarkKind == BenchmarkPriceKind.DocumentAnchorPrice
                ? lastWeb?.RejectionReasons
                : null);
    }

    private static string ClassifyConfidence(MarketPriceResearchResult r)
    {
        if (r.BenchmarkKind == BenchmarkPriceKind.DocumentAnchorPrice)
            return "document-anchor-only";

        if (r.ConfidenceScore is { } cs)
            return BenchmarkConfidenceCalculator.LegacyConfidenceLabel(cs, r.BenchmarkKind);

        if (r.Source.Contains("SerpApi:GoogleShopping", StringComparison.OrdinalIgnoreCase)
            && r.SampleCount >= 3
            && r.RelativeSpread is > 0 and < 0.6m)
            return "medium";

        if (r.Source.Contains("SerpApi", StringComparison.OrdinalIgnoreCase))
            return "low";

        if (r.SampleCount >= 6 && r.RelativeSpread is > 0 and < 0.45m)
            return "medium";

        return "low";
    }

    private async Task PersistMarketObservationAsync(
        MarketPrice quote,
        string? evidenceSnippet,
        CancellationToken ct)
    {
        try
        {
            var name = quote.ProductName;
            var category = _classifier.Classify(name);
            var region = GeographicRegion.DefaultSeedRegion;

            var existing = await _repository.GetByProductNameAsync(name, ct);
            if (existing is not null)
            {
                if (existing.Price != quote.Price)
                    existing.UpdatePrice(quote.Price);
                await _repository.UpdateAsync(existing, ct);
            }
            else
            {
                var obs = MarketPrice.CreateObservation(
                    name,
                    quote.Price,
                    ClampPersistedSource(quote.Source),
                    category,
                    region);
                await _repository.AddAsync(obs, ct);
            }

            var src = string.IsNullOrEmpty(evidenceSnippet)
                ? quote.Source
                : $"{quote.Source} | {evidenceSnippet.Trim()}";

            await _historyRepository.AddAsync(
                MarketPriceHistory.Create(name, quote.Price, ClampPersistedSource(src), DateTime.UtcNow),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir cotação de mercado para {ProductName}", quote.ProductName);
        }
    }

    public async Task<IEnumerable<MarketPriceHistory>> GetPriceHistoryAsync(string productName, int days, CancellationToken ct)
    {
        var persisted = (await _historyRepository.GetByProductNameAsync(productName, days, ct)).ToList();
        if (persisted.Count > 0)
        {
            _logger.LogInformation(
                "Histórico em PostgreSQL: {Count} pontos para {ProductName}",
                persisted.Count,
                productName);
            return persisted.OrderByDescending(h => h.CollectedDate);
        }

        return Array.Empty<MarketPriceHistory>();
    }
}
