using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simcag.MarketDataService.Application.Configuration;
using Simcag.MarketDataService.Application.Interfaces;

namespace Simcag.MarketDataService.Application.Services;

/// <summary>
/// Prioriza scraping local (HTML público: DuckDuckGo lite, Bing). Opcionalmente SerpAPI se houver chave.
/// Não gera preços — apenas interpreta texto público (mediana de menções a R$ plausíveis).
/// </summary>
public sealed class OnlineMarketPriceResearchService : IMarketPriceResearchService
{
    public const string HttpClientWebScrape = "MarketDataWebScrape";
    public const string HttpClientSerp = "MarketDataSerpApi";

    private readonly MarketResearchOptions _opt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OnlineMarketPriceResearchService> _log;

    private const string SerpApiBase = "https://serpapi.com/search.json";

    public OnlineMarketPriceResearchService(
        IOptions<MarketResearchOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<OnlineMarketPriceResearchService> log)
    {
        _opt = options.Value;
        _httpFactory = httpFactory;
        _log = log;
    }

    public async Task<MarketPriceResearchResult?> TryResolvePriceAsync(string productQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productQuery))
            return null;

        var q = BuildSearchQuery(productQuery.Trim());

        if (_opt.EnableDuckDuckGoLiteScrape)
        {
            var fromDdg = await TryDuckDuckGoLiteAsync(q, cancellationToken);
            if (fromDdg is not null)
                return fromDdg;
            var fromDdgHtml = await TryDuckDuckGoHtmlAsync(q, cancellationToken);
            if (fromDdgHtml is not null)
                return fromDdgHtml;
        }

        if (_opt.EnableBingHtmlScrape)
        {
            var fromBing = await TryBingHtmlAsync(q, cancellationToken);
            if (fromBing is not null)
                return fromBing;
        }

        if (!string.IsNullOrWhiteSpace(_opt.SerpApiKey))
        {
            var fromSerp = await TrySerpApiAsync(q, cancellationToken);
            if (fromSerp is not null)
                return fromSerp;
        }
        else
        {
            _log.LogDebug(
                "SerpAPI não configurado (MARKET_DATA__SERPAPI_API_KEY / SERPAPI_API_KEY vazio); fallback pago omitido.");
        }

        return null;
    }

    private static string BuildSearchQuery(string raw) =>
        raw.Contains("preço", StringComparison.OrdinalIgnoreCase) ||
        raw.Contains("preco", StringComparison.OrdinalIgnoreCase)
            ? raw + " Brasil"
            : raw + " preço Brasil";

    private async Task<MarketPriceResearchResult?> TryDuckDuckGoLiteAsync(string searchQuery, CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientWebScrape);
            var url = "https://lite.duckduckgo.com/lite/?q=" + Uri.EscapeDataString(searchQuery);
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            // 202 do DDG lite costuma ser página de bloqueio; IsSuccessStatusCode seria true — exigir 200.
            if (resp.StatusCode != HttpStatusCode.OK)
                return null;

            var html = await resp.Content.ReadAsStringAsync(ct);
            var text = ExtractDuckDuckGoLiteResultText(html);
            var extracted = BrazilianMoneyParser.ExtractAll(text);
            var median = BrazilianMoneyParser.Median(extracted);
            if (median is null)
                return null;

            return new MarketPriceResearchResult(median.Value, "DuckDuckGoLite:Snippets",
                "Mediana de menções a valores em BRL nos trechos de resultado (fonte agregada pública).");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha na pesquisa DuckDuckGo lite para {Query}", searchQuery);
            return null;
        }
    }

    private static string ExtractDuckDuckGoLiteResultText(string html)
    {
        try
        {
            var doc = new HtmlParser().ParseDocument(html);
            var links = doc.QuerySelector("#links");
            if (links is not null)
                return links.TextContent ?? string.Empty;
            var results = doc.QuerySelector(".results");
            if (results is not null)
                return results.TextContent ?? string.Empty;
        }
        catch
        {
            // fallback abaixo
        }

        return html;
    }

    private async Task<MarketPriceResearchResult?> TryDuckDuckGoHtmlAsync(string searchQuery, CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientWebScrape);
            var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(searchQuery);
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode != HttpStatusCode.OK)
                return null;

            var html = await resp.Content.ReadAsStringAsync(ct);
            var text = ExtractDuckDuckGoHtmlResultText(html);
            var extracted = BrazilianMoneyParser.ExtractAll(text);
            var median = BrazilianMoneyParser.Median(extracted);
            if (median is null)
                return null;

            return new MarketPriceResearchResult(median.Value, "DuckDuckGoHtml:Snippets",
                "Mediana de menções a valores em BRL nos resultados HTML do DuckDuckGo.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha na pesquisa DuckDuckGo HTML para {Query}", searchQuery);
            return null;
        }
    }

    private static string ExtractDuckDuckGoHtmlResultText(string html)
    {
        try
        {
            var doc = new HtmlParser().ParseDocument(html);
            var results = doc.QuerySelector(".results") ?? doc.QuerySelector("#links");
            if (results is not null)
                return results.TextContent ?? string.Empty;
        }
        catch
        {
            // fallback
        }

        return html;
    }

    private async Task<MarketPriceResearchResult?> TryBingHtmlAsync(string searchQuery, CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientWebScrape);
            var url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(searchQuery)
                      + "&cc=BR&setlang=pt-br&mkt=pt-BR";
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode != HttpStatusCode.OK)
                return null;

            var html = await resp.Content.ReadAsStringAsync(ct);
            var text = ExtractBingOrganicSnippets(html);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var extracted = BrazilianMoneyParser.ExtractAll(text);
            var median = BrazilianMoneyParser.Median(extracted);
            if (median is null)
                return null;

            return new MarketPriceResearchResult(median.Value, "BingHtml:OrganicSnippets",
                "Mediana de menções a valores em BRL nos snippets orgânicos retornados.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha na pesquisa Bing HTML para {Query}", searchQuery);
            return null;
        }
    }

    private static string ExtractBingOrganicSnippets(string html)
    {
        try
        {
            var doc = new HtmlParser().ParseDocument(html);
            var algos = doc.QuerySelectorAll("li.b_algo");
            var sb = new StringBuilder();
            if (algos.Length > 0)
            {
                foreach (var li in algos)
                    sb.AppendLine(li.TextContent);
            }
            else
            {
                var block = doc.QuerySelector("#b_results");
                if (block is not null)
                    sb.AppendLine(block.TextContent);
            }

            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<MarketPriceResearchResult?> TrySerpApiAsync(string searchQuery, CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientSerp);

            using (var shoppingDoc = await FetchSerpJsonAsync(client, BuildSerpUrl("google_shopping", searchQuery), ct))
            {
                var fromShopping = ParseSerpShopping(shoppingDoc);
                if (fromShopping is not null)
                    return fromShopping;
            }

            using var organicDoc = await FetchSerpJsonAsync(client, BuildSerpUrl("google", searchQuery), ct);
            return ParseSerpOrganic(organicDoc);
        }
        catch (Exception ex)
        {
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

    private async Task<JsonDocument?> FetchSerpJsonAsync(HttpClient client, string url, CancellationToken ct)
    {
        using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("SerpAPI HTTP {Code}", resp.StatusCode);
            return null;
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private MarketPriceResearchResult? ParseSerpShopping(JsonDocument? doc)
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

        var median = BrazilianMoneyParser.Median(amounts);
        if (median is null || median.Value <= 0m)
            return null;

        return new MarketPriceResearchResult(median.Value, "SerpApi:GoogleShopping", snippet);
    }

    private MarketPriceResearchResult? ParseSerpOrganic(JsonDocument? doc)
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
        var median = BrazilianMoneyParser.Median(extracted);
        if (median is null)
            return null;

        var ev = text.Length > 280 ? text[..280] : text;
        return new MarketPriceResearchResult(median.Value, "SerpApi:GoogleOrganic", ev);
    }
}
