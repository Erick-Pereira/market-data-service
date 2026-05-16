using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Benchmarking.Providers;
using Simcag.MarketDataService.Application.Configuration;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.Shared.Telemetry;

namespace Simcag.MarketDataService.Application.Services;

/// <summary>
/// Orquestra providers de amostragem (DDG/Bing), agregação com mediana e SerpAPI opcional.
/// </summary>
public sealed class OnlineMarketPriceResearchService : IMarketPriceResearchService
{
    /// <summary>Compatível com registos DI existentes.</summary>
    public const string HttpClientWebScrape = MarketDataHttpClients.WebScrape;

    public const string HttpClientSerp = MarketDataHttpClients.Serp;

    private const string SerpApiBase = "https://serpapi.com/search.json";

    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OnlineMarketPriceResearchService> _log;
    private readonly IReadOnlyList<IMarketPriceSampleProvider> _sampleProviders;

    public OnlineMarketPriceResearchService(
        IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<OnlineMarketPriceResearchService> log,
        DdgLiteMarketPriceProvider ddgLite,
        DdgHtmlMarketPriceProvider ddgHtml,
        BingHtmlMarketPriceProvider bingHtml)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _log = log;
        _sampleProviders = new IMarketPriceSampleProvider[] { ddgLite, ddgHtml, bingHtml };
    }

    public async Task<MarketPriceResearchResult?> TryResolvePriceAsync(
        string productQuery,
        CancellationToken cancellationToken = default)
    {
        var d = await TryResolvePriceDetailedAsync(productQuery, cancellationToken);
        return d.Result;
    }

    public async Task<MarketPriceResearchDetailedOutcome> TryResolvePriceDetailedAsync(
        string productQuery,
        CancellationToken cancellationToken = default)
    {
        var rejections = new List<string>();
        var diagnostics = new List<BenchmarkDiagnosticEntry>();

        if (string.IsNullOrWhiteSpace(productQuery))
        {
            rejections.Add("empty_query");
            diagnostics.Add(new BenchmarkDiagnosticEntry("orchestrator", "validate", "productQuery vazio", null));
            return new MarketPriceResearchDetailedOutcome(null, rejections, diagnostics);
        }

        using var root = SimcagActivitySources.MarketData.StartActivity("marketdata.price_research", ActivityKind.Internal);
        root?.SetTag("marketdata.query.length", productQuery.Trim().Length);
        var sw = Stopwatch.StartNew();

        var q = BuildSearchQuery(productQuery.Trim());

        var batches = await Task.WhenAll(_sampleProviders.Select(p => p.FetchSamplesAsync(q, cancellationToken)));
        foreach (var b in batches)
        {
            diagnostics.Add(new BenchmarkDiagnosticEntry(
                $"provider:{b.ProviderId}",
                "fetch",
                b.Outcome,
                b.Detail));
        }

        var merged = batches.SelectMany(b => b.Samples).ToList();
        var antiBot = batches.Any(b => b.AntiBotLikely);

        if (merged.Count > 0)
        {
            var fromAgg = BuildFromExtracted(
                merged,
                "WebScrape:Aggregated(DDG+Bing)",
                "Consolidação de valores em BRL extraídos dos snippets DuckDuckGo e Bing.",
                q,
                structuredPriceList: false,
                antiBotLikely: antiBot,
                rejections);
            if (fromAgg is not null)
            {
                RecordBenchmarkEnd(sw, success: true, source: "web_scrape");
                return new MarketPriceResearchDetailedOutcome(
                    fromAgg with { Diagnostics = diagnostics },
                    rejections,
                    diagnostics);
            }
        }
        else
        {
            rejections.Add("web_scrape_no_price_samples");
            SimcagMeters.MarketDataBenchmarkRejections.Add(1,
                new KeyValuePair<string, object?>("reason", "no_samples"),
                new KeyValuePair<string, object?>("stage", "aggregate"));
        }

        if (!string.IsNullOrWhiteSpace(_opt.SerpApiKey))
        {
            using (SimcagActivitySources.MarketData.StartActivity("marketdata.serpapi", ActivityKind.Client))
            {
                var fromSerp = await TrySerpApiAsync(q, diagnostics, rejections, cancellationToken);
                if (fromSerp is not null)
                {
                    RecordBenchmarkEnd(sw, success: true, source: "serp");
                    return new MarketPriceResearchDetailedOutcome(
                        fromSerp with { Diagnostics = diagnostics },
                        rejections,
                        diagnostics);
                }
            }
        }
        else
        {
            diagnostics.Add(new BenchmarkDiagnosticEntry("serpapi", "config", "SerpAPI omitida (sem chave)", null));
            _log.LogDebug(
                "SerpAPI não configurada (MARKET_DATA__SERPAPI_API_KEY / SERPAPI_API_KEY vazio); fallback pago omitido.");
        }

        if (rejections.Count == 0)
            rejections.Add("no_usable_external_estimate");

        RecordBenchmarkEnd(sw, success: false, source: "none");
        _log.LogInformation(
            "Pesquisa de preço sem resultado utilizável (Query={Query}); rejeições={Rejections}",
            q,
            string.Join(",", rejections));
        return new MarketPriceResearchDetailedOutcome(null, rejections, diagnostics);
    }

    private void RecordBenchmarkEnd(Stopwatch sw, bool success, string source)
    {
        var elapsed = sw.Elapsed.TotalSeconds;
        SimcagMeters.MarketDataBenchmarkDurationSeconds.Record(elapsed,
            new KeyValuePair<string, object?>("success", success),
            new KeyValuePair<string, object?>("source", source));
        if (success)
            SimcagMeters.MarketDataBenchmarkSuccess.Add(1, new KeyValuePair<string, object?>("source", source));
        else
            SimcagMeters.MarketDataBenchmarkEmpty.Add(1, new KeyValuePair<string, object?>("source", source));
    }

    private static string BuildSearchQuery(string raw) =>
        raw.Contains("preço", StringComparison.OrdinalIgnoreCase) ||
        raw.Contains("preco", StringComparison.OrdinalIgnoreCase)
            ? raw + " Brasil"
            : raw + " preço Brasil";

    private MarketPriceResearchResult? BuildFromExtracted(
        IReadOnlyList<decimal> extracted,
        string source,
        string? evidenceSnippet,
        string searchQueryUsed,
        bool structuredPriceList,
        bool antiBotLikely,
        List<string> rejections)
    {
        var minSamples = structuredPriceList
            ? Math.Max(1, _opt.MinimumPriceSamplesForStructuredList)
            : Math.Max(1, _opt.MinimumPriceSamplesForWebScrape);
        var distinctPrices = extracted.Distinct().ToList();
        if (distinctPrices.Count < minSamples)
        {
            rejections.Add(BenchmarkStatuses.RejectedInsufficientSamples);
            SimcagMeters.MarketDataBenchmarkRejections.Add(1,
                new KeyValuePair<string, object?>("reason", "insufficient_samples"),
                new KeyValuePair<string, object?>("stage", structuredPriceList ? "structured" : "web_scrape"));
            _log.LogInformation(
                "Cotação rejeitada: amostras insuficientes (distinct={Distinct}, min={Min}, structured={Structured})",
                distinctPrices.Count,
                minSamples,
                structuredPriceList);
            return null;
        }

        if (_opt.RequireDistinctSamplesForWebScrape && !structuredPriceList && distinctPrices.Count < 2)
        {
            rejections.Add(BenchmarkStatuses.RejectedDistinctSamples);
            SimcagMeters.MarketDataBenchmarkRejections.Add(1,
                new KeyValuePair<string, object?>("reason", "distinct_samples_policy"),
                new KeyValuePair<string, object?>("stage", "web_scrape"));
            _log.LogInformation(
                "Cotação web rejeitada: política exige ≥2 preços distintos (distinct={Distinct})",
                distinctPrices.Count);
            return null;
        }

        var median = BrazilianMoneyParser.Median(distinctPrices);
        if (median is null)
            return null;

        var spread = BrazilianMoneyParser.RelativeSpreadAroundMedian(distinctPrices, median.Value);
        if (!structuredPriceList && distinctPrices.Count >= 2 && spread > _opt.MaxRelativeSpreadForWebScrape)
        {
            rejections.Add(BenchmarkStatuses.RejectedSpread);
            SimcagMeters.MarketDataBenchmarkRejections.Add(1,
                new KeyValuePair<string, object?>("reason", "spread"),
                new KeyValuePair<string, object?>("stage", "web_scrape"));
            _log.LogInformation(
                "Cotação web rejeitada por dispersão (spread={Spread:F2} > {Max}, amostras={Count}, fonte={Source})",
                spread,
                _opt.MaxRelativeSpreadForWebScrape,
                distinctPrices.Count,
                source);
            return null;
        }

        var (confidence, quality) = BenchmarkConfidenceCalculator.Compute(
            distinctPrices.Count,
            spread,
            structuredPriceList,
            antiBotLikely);

        return new MarketPriceResearchResult(
            median.Value,
            source,
            evidenceSnippet,
            distinctPrices.Count,
            spread,
            searchQueryUsed,
            BenchmarkPriceKind.ExternalMarketEstimate,
            BenchmarkStatuses.ResolvedExternal,
            confidence,
            quality,
            Diagnostics: null);
    }

    private async Task<MarketPriceResearchResult?> TrySerpApiAsync(
        string searchQuery,
        List<BenchmarkDiagnosticEntry> diagnostics,
        List<string> rejections,
        CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientSerp);

            using (var shoppingDoc = await FetchSerpJsonAsync(client, BuildSerpUrl("google_shopping", searchQuery), diagnostics, ct))
            {
                var fromShopping = ParseSerpShopping(shoppingDoc, searchQuery, rejections);
                if (fromShopping is not null)
                    return fromShopping;
            }

            using var organicDoc = await FetchSerpJsonAsync(client, BuildSerpUrl("google", searchQuery), diagnostics, ct);
            return ParseSerpOrganic(organicDoc, searchQuery, rejections);
        }
        catch (Exception ex)
        {
            SimcagMeters.MarketDataScrapeHttpErrors.Add(1,
                new KeyValuePair<string, object?>("source", "serpapi"),
                new KeyValuePair<string, object?>("reason", "exception"));
            diagnostics.Add(new BenchmarkDiagnosticEntry("serpapi", "exception", ex.GetType().Name, ex.Message));
            _log.LogWarning(ex, "Falha na pesquisa SerpAPI para {Query}", searchQuery);
            return null;
        }
    }

    private string BuildSerpUrl(string engine, string q)
    {
        static string E(string? s) => Uri.EscapeDataString(s ?? string.Empty);
        var key = E(_opt.SerpApiKey);
        return $"{SerpApiBase}?engine={E(engine)}&q={E(q)}&api_key={key}&hl=pt&gl=br&google_domain=google.com.br";
    }

    private async Task<JsonDocument?> FetchSerpJsonAsync(
        HttpClient client,
        string url,
        List<BenchmarkDiagnosticEntry> diagnostics,
        CancellationToken ct)
    {
        using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resp.StatusCode == HttpStatusCode.Accepted)
        {
            SimcagMeters.MarketDataScrapeHttpAccepted.Add(1,
                new KeyValuePair<string, object?>("provider", "serpapi"));
            diagnostics.Add(new BenchmarkDiagnosticEntry("serpapi", "http", "202 Accepted", url));
            return null;
        }

        if (!resp.IsSuccessStatusCode)
        {
            SimcagMeters.MarketDataScrapeHttpErrors.Add(1,
                new KeyValuePair<string, object?>("source", "serpapi"),
                new KeyValuePair<string, object?>("status_code", (int)resp.StatusCode));
            diagnostics.Add(new BenchmarkDiagnosticEntry("serpapi", "http", "not_success", ((int)resp.StatusCode).ToString()));
            _log.LogWarning("SerpAPI HTTP {Code}", resp.StatusCode);
            return null;
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private MarketPriceResearchResult? ParseSerpShopping(JsonDocument? doc, string searchQueryUsed, List<string> rejections)
    {
        if (doc is null)
            return null;

        if (doc.RootElement.TryGetProperty("error", out var serpErr))
        {
            _log.LogWarning("SerpAPI respondeu erro: {Message}", serpErr.ToString());
            return null;
        }

        if (!doc.RootElement.TryGetProperty("shopping_results", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var amounts = new List<decimal>();
        string? snippet = null;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.TryGetProperty("extracted_price", out var ep))
            {
                if (ep.ValueKind == JsonValueKind.Number && ep.TryGetDecimal(out var d) && d > 0m)
                    amounts.Add(d);
                else if (ep.ValueKind == JsonValueKind.String
                         && decimal.TryParse(ep.GetString(), System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out var ds))
                    amounts.Add(ds);
            }

            if (item.TryGetProperty("price", out var priceStr) && priceStr.ValueKind == JsonValueKind.String)
            {
                foreach (var v in BrazilianMoneyParser.ExtractAll(priceStr.GetString()))
                    amounts.Add(v);
            }

            snippet ??= item.TryGetProperty("snippet", out var sn)
                ? sn.GetString()
                : item.TryGetProperty("title", out var t)
                    ? t.GetString()
                    : null;
        }

        return BuildFromExtracted(amounts, "SerpApi:GoogleShopping", snippet, searchQueryUsed, structuredPriceList: true,
            antiBotLikely: false, rejections);
    }

    private MarketPriceResearchResult? ParseSerpOrganic(JsonDocument? doc, string searchQueryUsed, List<string> rejections)
    {
        if (doc is null)
            return null;

        if (doc.RootElement.TryGetProperty("error", out var serpErr))
        {
            _log.LogWarning("SerpAPI respondeu erro: {Message}", serpErr.ToString());
            return null;
        }

        if (!doc.RootElement.TryGetProperty("organic_results", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;

        var chunks = new List<string?>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.TryGetProperty("snippet", out var sn))
                chunks.Add(sn.GetString());
            if (item.TryGetProperty("title", out var t))
                chunks.Add(t.GetString());
        }

        var text = string.Join(" ", chunks.Where(s => !string.IsNullOrEmpty(s)));
        var extracted = BrazilianMoneyParser.ExtractAll(text);
        var ev = text.Length > 280 ? text[..280] : text;
        return BuildFromExtracted(extracted, "SerpApi:GoogleOrganic", ev, searchQueryUsed, structuredPriceList: false,
            antiBotLikely: false, rejections);
    }
}
