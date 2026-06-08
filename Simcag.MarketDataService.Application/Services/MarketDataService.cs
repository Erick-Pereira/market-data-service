using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Classification;
using Simcag.MarketDataService.Application.Configuration;
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
    private readonly MarketResearchOptions _researchOptions;

    public MarketDataService(
        ILogger<MarketDataService> logger,
        IMarketDataCacheService cacheService,
        IMarketPriceRepository repository,
        IMarketPriceHistoryRepository historyRepository,
        IMarketPriceResearchService research,
        IRuleBasedExpenseCategoryClassifier classifier,
        IOptions<MarketResearchOptions> researchOptions)
    {
        _logger = logger;
        _cacheService = cacheService;
        _repository = repository;
        _historyRepository = historyRepository;
        _research = research;
        _classifier = classifier;
        _researchOptions = researchOptions.Value;
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

        MarketPrice? staleExternalCandidate = null;

        foreach (var searchName in searchNames)
        {
            var fromDb = await _repository.GetByProductNameAsync(searchName, ct);
            if (fromDb is null)
                continue;

            if (StoredMarketPricePolicy.IsExternalBenchmarkSource(fromDb.Source))
                staleExternalCandidate ??= fromDb;

            if (!StoredMarketPricePolicy.ShouldRefresh(declaredReferenceBrl, fromDb))
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

            _logger.LogInformation(
                "Preço em PostgreSQL ignorado para {ProductName} ({StoredPrice} vs declarado {Declared}); tentando pesquisa web.",
                searchName,
                fromDb.Price,
                declaredReferenceBrl);
        }

        MarketPriceResearchDetailedOutcome? lastWeb = null;
        MarketPriceResearchResult? researched = null;
        foreach (var query in searchNames)
        {
            lastWeb = await _research.TryResolvePriceDetailedAsync(query, declaredReferenceBrl, ct);
            if (lastWeb.Result is not null)
            {
                researched = lastWeb.Result;
                break;
            }
        }

        if (researched is null && _researchOptions.EnableCuratedCategoryBenchmark)
        {
            researched = TryCuratedCategoryBenchmark(trimmed, declaredReferenceBrl);
            if (researched is not null)
            {
                _logger.LogInformation(
                    "Benchmark curado para {ProductName}: {Price:C} ({Source})",
                    productName,
                    researched.Price,
                    researched.Source);
            }
        }

        if (researched is null
            && staleExternalCandidate is not null
            && declaredReferenceBrl is > 0.01m
            && StoredMarketPricePolicy.ShouldRefresh(declaredReferenceBrl, staleExternalCandidate))
        {
            var stalePrice = Math.Round(staleExternalCandidate.Price, 2);
            researched = new MarketPriceResearchResult(
                stalePrice,
                ClampPersistedSource($"PostgreSQL:StaleFallback({staleExternalCandidate.Source})"),
                "Benchmark externo anterior em PostgreSQL (pesquisa web indisponível ou bloqueada).",
                1,
                0,
                null,
                BenchmarkPriceKind.ExternalMarketEstimate,
                BenchmarkStatuses.DatabaseHit,
                0.35m,
                0.25m,
                new[]
                {
                    new BenchmarkDiagnosticEntry(
                        "stale_external_fallback",
                        "postgresql",
                        "Web scrape sem amostras; reutilizando cotação externa divergente do declarado.",
                        stalePrice.ToString(System.Globalization.CultureInfo.InvariantCulture))
                });
            _logger.LogWarning(
                "Fallback PostgreSQL (stale external) para {ProductName}: {StoredPrice} vs declarado {Declared}; rejeições web={Rejections}",
                productName,
                stalePrice,
                declaredReferenceBrl,
                lastWeb is null ? "" : string.Join(";", lastWeb.RejectionReasons));
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

    /// <summary>
    /// Atualiza benchmark de preço para produto já cadastrado no sistema.
    /// Usado por cron jobs ou triggers do processing-service.
    /// </summary>
    public async Task<MarketPriceResolution?> UpdateBenchmarkForExistingProductAsync(
        Guid productId, 
        string productName, 
        CancellationToken ct)
    {
        _logger.LogInformation("Atualizando benchmark para produto cadastrado: {ProductId} - {ProductName}", productId, productName);

        // 1. Buscar produto cadastrado no sistema (simulação - em produção usar repository real)
        var product = await GetProductByIdAsync(productId, ct);
        if (product == null)
        {
            _logger.LogWarning("Produto com ID {ProductId} não encontrado no catálogo", productId);
            return null;
        }

        // 2. Coletar preço apenas para este produto específico via pesquisa web
        var researchResult = await _research.TryResolvePriceAsync(productName, declaredReferenceBrl: null, ct);

        // 3. Atualizar cache e persistir se houver resultado
        if (researchResult != null)
        {
            // Criar MarketPrice a partir do researchResult
            var quote = MarketPrice.Create(
                productName, 
                Math.Round(researchResult.Price, 2), 
                researchResult.Source);

            await _cacheService.SetMarketPriceAsync(quote, ct);
            await PersistMarketObservationAsync(quote, researchResult.EvidenceSnippet, ct);

            // 4. Atualizar produto cadastrado com novo preço de benchmark
            product.UpdateBenchmark(researchResult.Price);
            
            _logger.LogInformation(
                "Benchmark atualizado para produto {ProductId}: {ProductName} - Preço: {Price:C}",
                productId, productName, researchResult.Price);

            return new MarketPriceResolution(
                quote,
                researchResult.SampleCount,
                researchResult.RelativeSpread,
                researchResult.SearchQueryUsed,
                productName,
                ClassifyConfidence(researchResult),
                researchResult.BenchmarkKind,
                researchResult.BenchmarkStatus,
                researchResult.ConfidenceScore,
                researchResult.BenchmarkQualityScore,
                researchResult.Diagnostics,
                null);
        }
        else
        {
            _logger.LogWarning("Pesquisa de preço falhou para produto cadastrado {ProductId}: {ProductName}", productId, productName);
            return null;
        }
    }

    private async Task<ProductCatalogEntry?> GetProductByIdAsync(Guid productId, CancellationToken ct)
    {
        // Em produção, isso deve usar um repository real do ProductCatalog
        // Atualmente retorna null para indicar que o produto não está cadastrado
        // O processing-service deve garantir que produtos existentes estejam no catálogo antes de chamar este endpoint
        return null;
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

    private static MarketPriceResearchResult? TryCuratedCategoryBenchmark(
        string productName,
        decimal? declaredReferenceBrl)
    {
        var match = CuratedCategoryBenchmarkCatalog.TryMatch(productName, declaredReferenceBrl);
        if (match is null)
            return null;

        return new MarketPriceResearchResult(
            Math.Round(match.ReferencePriceBrl, 2),
            $"{CuratedCategoryBenchmarkCatalog.SourcePrefix}:{match.PatternId}",
            match.EvidenceSnippet,
            1,
            0,
            null,
            BenchmarkPriceKind.ExternalMarketEstimate,
            BenchmarkStatuses.DatabaseHit,
            0.45m,
            0.40m,
            new[]
            {
                new BenchmarkDiagnosticEntry(
                    "curated_catalog",
                    "pattern_match",
                    match.PatternId,
                    match.Category)
            });
    }

    private async Task PersistMarketObservationAsync(
        MarketPrice quote,
        string? evidenceSnippet,
        CancellationToken ct)
    {
        if (StoredMarketPricePolicy.IsDocumentAnchorSource(quote.Source))
            return;

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
