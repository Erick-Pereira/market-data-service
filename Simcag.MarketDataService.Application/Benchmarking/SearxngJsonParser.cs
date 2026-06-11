using System.Text;
using System.Text.Json;
using Simcag.MarketDataService.Application.Services;
using Simcag.Shared.Contracts;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Extrai título + snippet dos resultados JSON do SearXNG.</summary>
internal static class SearxngJsonParser
{
    public sealed record SearchResultLink(string Url, string Title);

    public static string ExtractResultText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var link in ExtractTopResults(json, 20))
        {
            if (!string.IsNullOrWhiteSpace(link.Title))
                sb.Append(link.Title).Append(' ');
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return sb.ToString();

            foreach (var item in results.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    var t = title.GetString();
                    if (!string.IsNullOrWhiteSpace(t))
                        sb.Append(t).Append(' ');
                }

                if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    sb.Append(content.GetString()).Append(' ');
            }
        }
        catch (JsonException)
        {
            // ignore — titles from ExtractTopResults already collected
        }

        return sb.ToString();
    }

    /// <summary>Top N resultados com URL para trilha de prova real.</summary>
    public static IReadOnlyList<SearchResultLink> ExtractTopResults(string? json, int max = 5)
    {
        if (string.IsNullOrWhiteSpace(json) || max < 1)
            return Array.Empty<SearchResultLink>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return Array.Empty<SearchResultLink>();

            var list = new List<SearchResultLink>();
            foreach (var item in results.EnumerateArray())
            {
                if (list.Count >= max)
                    break;

                var url = item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                    ? u.GetString() ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? string.Empty
                    : string.Empty;

                list.Add(new SearchResultLink(url.Trim(), title.Trim()));
            }

            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<SearchResultLink>();
        }
    }

    /// <summary>Serializa links para o campo Detail do provider (parseado por <see cref="MarketReferenceLinkBuilder"/>).</summary>
    public static string? FormatTopResultsDetail(IReadOnlyList<SearchResultLink> links)
    {
        if (links is not { Count: > 0 })
            return null;

        var parts = links
            .Where(l => !string.IsNullOrWhiteSpace(l.Url))
            .Select(l => $"{l.Url}|{l.Title.Replace('|', ' ')}");
        var joined = string.Join(";", parts);
        return string.IsNullOrEmpty(joined) ? null : "top_results=" + joined;
    }

    /// <summary>Extrai preço por resultado (título + snippet) para prova auditável com link.</summary>
    public static IReadOnlyList<MarketPriceSample> ExtractResultPriceSamples(string? json, int max = 5)
    {
        if (string.IsNullOrWhiteSpace(json) || max < 1)
            return Array.Empty<MarketPriceSample>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return Array.Empty<MarketPriceSample>();

            var list = new List<MarketPriceSample>();
            foreach (var item in results.EnumerateArray())
            {
                if (list.Count >= max)
                    break;

                var url = item.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                    ? u.GetString() ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? string.Empty
                    : string.Empty;
                var content = item.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString() ?? string.Empty
                    : string.Empty;

                var text = $"{title} {content}".Trim();
                var price = BrazilianMoneyParser.SelectListingPrice(text);

                var label = !string.IsNullOrWhiteSpace(title)
                    ? Truncate(title.Trim(), 72)
                    : TryHostLabel(url);

                list.Add(new MarketPriceSample
                {
                    Label = label,
                    Url = url.Trim(),
                    PriceBrl = price,
                    Provider = "searxng",
                });
            }

            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<MarketPriceSample>();
        }
    }

    private static string TryHostLabel(string url)
    {
        try
        {
            var host = new Uri(url).Host;
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        }
        catch (UriFormatException)
        {
            return "Resultado web";
        }
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
