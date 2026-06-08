using System.Net;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application;
using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Configuration;
using Simcag.MarketDataService.Application.Services;
using Simcag.Shared.Telemetry;

namespace Simcag.MarketDataService.Application.Benchmarking.Providers;

public interface IMarketPriceSampleProvider
{
    string ProviderId { get; }

    bool IsEnabled(MarketResearchOptions options);

    Task<ProviderSampleBatch> FetchSamplesAsync(string searchQuery, CancellationToken ct);
}

public sealed record ProviderSampleBatch(
    string ProviderId,
    IReadOnlyList<decimal> Samples,
    HttpStatusCode? HttpStatus,
    string Outcome,
    string? Detail,
    bool AntiBotLikely);

public sealed class SearxngMarketPriceProvider : IMarketPriceSampleProvider
{
    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly SearxngAvailabilityGate _gate;
    private readonly ILogger _log;

    public SearxngMarketPriceProvider(
        Microsoft.Extensions.Options.IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        SearxngAvailabilityGate gate,
        ILogger<SearxngMarketPriceProvider> log)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _gate = gate;
        _log = log;
    }

    public string ProviderId => "searxng";

    public bool IsEnabled(MarketResearchOptions options) =>
        options.EnableSearxngScrape && !string.IsNullOrWhiteSpace(options.SearxngBaseUrl);

    public async Task<ProviderSampleBatch> FetchSamplesAsync(string searchQuery, CancellationToken ct)
    {
        if (!IsEnabled(_opt))
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), null, "disabled", null, false);

        if (!_gate.IsAvailable)
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), null, "circuit_open", "searxng_cooldown", false);

        var baseUrl = _opt.SearxngBaseUrl!.Trim().TrimEnd('/');
        SimcagMeters.MarketDataProviderFetchAttempts.Add(1,
            new KeyValuePair<string, object?>("provider", ProviderId),
            new KeyValuePair<string, object?>("phase", "start"));

        var client = _httpFactory.CreateClient(MarketDataHttpClients.Searxng);
        var url = baseUrl + "/search?q=" + Uri.EscapeDataString(searchQuery)
                  + "&format=json&language=pt-BR&categories=general";
        var (status, body, outcome) = await MarketScrapeHttp.GetHtmlAsync(
            client, url, _opt, ProviderId, _log, ct, maxRetriesOverride: 0);
        if (outcome != "ok" || string.IsNullOrEmpty(body))
        {
            if (outcome == "connection_refused")
            {
                _gate.MarkUnavailable(TimeSpan.FromMinutes(Math.Max(1, _opt.SearxngUnavailableCooldownMinutes)));
                if (_gate.TryLogStartupWarningOnce())
                {
                    _log.LogWarning(
                        "SearXNG indisponível em {BaseUrl}. Pesquisa continua via DDG/Bing. Suba: docker compose -f docker-compose.dev.yml up -d searxng",
                        baseUrl);
                }
            }

            var detail = outcome switch
            {
                "http_not_success" when status is { } s => $"status={(int)s}",
                "connection_refused" => "searxng_unreachable",
                _ => outcome
            };
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, outcome, detail, false);
        }

        _gate.MarkAvailable();
        var text = SearxngJsonParser.ExtractResultText(body);
        var samples = BrazilianMoneyParser.ExtractAll(text);
        return new ProviderSampleBatch(ProviderId, samples, status, samples.Count > 0 ? "ok" : "parse_empty",
            null, false);
    }
}

public sealed class DdgLiteMarketPriceProvider : IMarketPriceSampleProvider
{
    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger _log;

    public DdgLiteMarketPriceProvider(
        Microsoft.Extensions.Options.IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<DdgLiteMarketPriceProvider> log)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _log = log;
    }

    public string ProviderId => "ddg_lite";

    public bool IsEnabled(MarketResearchOptions options) => options.EnableDuckDuckGoLiteScrape;

    public async Task<ProviderSampleBatch> FetchSamplesAsync(string searchQuery, CancellationToken ct)
    {
        if (!IsEnabled(_opt))
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), null, "disabled", null, false);

        SimcagMeters.MarketDataProviderFetchAttempts.Add(1,
            new KeyValuePair<string, object?>("provider", ProviderId),
            new KeyValuePair<string, object?>("phase", "start"));

        var client = _httpFactory.CreateClient(MarketDataHttpClients.WebScrape);
        var url = "https://lite.duckduckgo.com/lite/?q=" + Uri.EscapeDataString(searchQuery);
        var (status, html, outcome) = await MarketScrapeHttp.GetHtmlAsync(client, url, _opt, ProviderId, _log, ct);
        if (outcome != "ok" || string.IsNullOrEmpty(html))
        {
            var detail = outcome == "http_not_success" && status is { } s ? $"status={(int)s}" : null;
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, outcome, detail, false);
        }

        if (ScrapeAntiBotHeuristics.LikelyBlocked(html, out var sig))
        {
            SimcagMeters.MarketDataScrapeAntiBotSignals.Add(1,
                new KeyValuePair<string, object?>("provider", ProviderId),
                new KeyValuePair<string, object?>("signal", sig ?? "unknown"));
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, "antibot_html",
                sig, AntiBotLikely: true);
        }

        var text = HtmlSnippetExtractors.DuckDuckGoLite(html, _log);
        var samples = BrazilianMoneyParser.ExtractAll(text);
        return new ProviderSampleBatch(ProviderId, samples, status, samples.Count > 0 ? "ok" : "parse_empty",
            null, false);
    }
}

public sealed class DdgHtmlMarketPriceProvider : IMarketPriceSampleProvider
{
    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger _log;

    public DdgHtmlMarketPriceProvider(
        Microsoft.Extensions.Options.IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<DdgHtmlMarketPriceProvider> log)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _log = log;
    }

    public string ProviderId => "ddg_html";

    public bool IsEnabled(MarketResearchOptions options) => options.EnableDuckDuckGoLiteScrape;

    public async Task<ProviderSampleBatch> FetchSamplesAsync(string searchQuery, CancellationToken ct)
    {
        if (!IsEnabled(_opt))
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), null, "disabled", null, false);

        SimcagMeters.MarketDataProviderFetchAttempts.Add(1,
            new KeyValuePair<string, object?>("provider", ProviderId),
            new KeyValuePair<string, object?>("phase", "start"));

        var client = _httpFactory.CreateClient(MarketDataHttpClients.WebScrape);
        var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(searchQuery);
        var (status, html, outcome) = await MarketScrapeHttp.GetHtmlAsync(client, url, _opt, ProviderId, _log, ct);
        if (outcome != "ok" || string.IsNullOrEmpty(html))
        {
            var detail = outcome == "http_not_success" && status is { } s ? $"status={(int)s}" : null;
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, outcome, detail, false);
        }

        if (ScrapeAntiBotHeuristics.LikelyBlocked(html, out var sig))
        {
            SimcagMeters.MarketDataScrapeAntiBotSignals.Add(1,
                new KeyValuePair<string, object?>("provider", ProviderId),
                new KeyValuePair<string, object?>("signal", sig ?? "unknown"));
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, "antibot_html",
                sig, AntiBotLikely: true);
        }

        var text = HtmlSnippetExtractors.DuckDuckGoHtml(html, _log);
        var samples = BrazilianMoneyParser.ExtractAll(text);
        return new ProviderSampleBatch(ProviderId, samples, status, samples.Count > 0 ? "ok" : "parse_empty",
            null, false);
    }
}

public sealed class BingHtmlMarketPriceProvider : IMarketPriceSampleProvider
{
    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger _log;

    public BingHtmlMarketPriceProvider(
        Microsoft.Extensions.Options.IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<BingHtmlMarketPriceProvider> log)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _log = log;
    }

    public string ProviderId => "bing_html";

    public bool IsEnabled(MarketResearchOptions options) => options.EnableBingHtmlScrape;

    public async Task<ProviderSampleBatch> FetchSamplesAsync(string searchQuery, CancellationToken ct)
    {
        if (!IsEnabled(_opt))
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), null, "disabled", null, false);

        SimcagMeters.MarketDataProviderFetchAttempts.Add(1,
            new KeyValuePair<string, object?>("provider", ProviderId),
            new KeyValuePair<string, object?>("phase", "start"));

        var client = _httpFactory.CreateClient(MarketDataHttpClients.WebScrape);
        var url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(searchQuery)
                  + "&cc=BR&setlang=pt-br&mkt=pt-BR";
        var (status, html, outcome) = await MarketScrapeHttp.GetHtmlAsync(client, url, _opt, ProviderId, _log, ct);
        if (outcome != "ok" || string.IsNullOrEmpty(html))
        {
            var detail = outcome == "http_not_success" && status is { } s ? $"status={(int)s}" : null;
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, outcome, detail, false);
        }

        if (ScrapeAntiBotHeuristics.LikelyBlocked(html, out var sig))
        {
            SimcagMeters.MarketDataScrapeAntiBotSignals.Add(1,
                new KeyValuePair<string, object?>("provider", ProviderId),
                new KeyValuePair<string, object?>("signal", sig ?? "unknown"));
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, "antibot_html",
                sig, AntiBotLikely: true);
        }

        var text = HtmlSnippetExtractors.BingOrganicSnippets(html, _log);
        if (string.IsNullOrWhiteSpace(text))
            text = HtmlSnippetExtractors.BingResultsFallbackText(html, _log);
        var samples = string.IsNullOrWhiteSpace(text) ? Array.Empty<decimal>() : BrazilianMoneyParser.ExtractAll(text);
        return new ProviderSampleBatch(ProviderId, samples, status, samples.Count > 0 ? "ok" : "parse_empty",
            null, false);
    }
}

public sealed class BingRssMarketPriceProvider : IMarketPriceSampleProvider
{
    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger _log;

    public BingRssMarketPriceProvider(
        Microsoft.Extensions.Options.IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<BingRssMarketPriceProvider> log)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _log = log;
    }

    public string ProviderId => "bing_rss";

    public bool IsEnabled(MarketResearchOptions options) => options.EnableBingRssScrape;

    public async Task<ProviderSampleBatch> FetchSamplesAsync(string searchQuery, CancellationToken ct)
    {
        if (!IsEnabled(_opt))
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), null, "disabled", null, false);

        SimcagMeters.MarketDataProviderFetchAttempts.Add(1,
            new KeyValuePair<string, object?>("provider", ProviderId),
            new KeyValuePair<string, object?>("phase", "start"));

        var client = _httpFactory.CreateClient(MarketDataHttpClients.WebScrape);
        var url = "https://www.bing.com/search?format=rss&q=" + Uri.EscapeDataString(searchQuery)
                  + "&cc=BR&setlang=pt-br&mkt=pt-BR";
        var (status, xml, outcome) = await MarketScrapeHttp.GetHtmlAsync(client, url, _opt, ProviderId, _log, ct);
        if (outcome != "ok" || string.IsNullOrEmpty(xml))
        {
            var detail = outcome == "http_not_success" && status is { } s ? $"status={(int)s}" : null;
            return new ProviderSampleBatch(ProviderId, Array.Empty<decimal>(), status, outcome, detail, false);
        }

        var text = RssSnippetExtractor.ExtractItemDescriptions(xml);
        var samples = BrazilianMoneyParser.ExtractAll(text);
        return new ProviderSampleBatch(ProviderId, samples, status, samples.Count > 0 ? "ok" : "parse_empty",
            null, false);
    }
}
