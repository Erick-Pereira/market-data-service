using System.Text.RegularExpressions;
using Simcag.Shared.Contracts;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Monta links reproduzíveis a partir da consulta e das evidências da pipeline.</summary>
public static class MarketReferenceLinkBuilder
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TopResultsPrefix = new(
        @"^top_results=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<MarketPriceReferenceLink> Build(
        string? searchQueryUsed,
        IReadOnlyList<BenchmarkDiagnosticEntry>? diagnostics,
        string? searxngBaseUrl = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<MarketPriceReferenceLink>();

        AddSearxngLocalLink(links, seen, searchQueryUsed, searxngBaseUrl);
        AddReproSearchLinks(links, seen, searchQueryUsed);
        AddLinksFromDiagnostics(links, seen, diagnostics);

        return links;
    }

    private static void AddSearxngLocalLink(
        List<MarketPriceReferenceLink> links,
        HashSet<string> seen,
        string? query,
        string? searxngBaseUrl)
    {
        var q = query?.Trim();
        var baseUrl = searxngBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(q) || string.IsNullOrEmpty(baseUrl))
            return;

        var url = baseUrl + "/search?q=" + Uri.EscapeDataString(q) + "&format=json&language=pt-BR";
        TryAdd(links, seen, "Consulta no SearXNG (instância local)", url);
    }

    private static void AddReproSearchLinks(List<MarketPriceReferenceLink> links, HashSet<string> seen, string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q))
            return;

        TryAdd(links, seen, "Reproduzir no Google", "https://www.google.com/search?q=" + Uri.EscapeDataString(q));
        TryAdd(links, seen, "Reproduzir no Bing", "https://www.bing.com/search?q=" + Uri.EscapeDataString(q));

        if (q.Contains("mercadolivre", StringComparison.OrdinalIgnoreCase))
        {
            var slug = q
                .Replace("site:mercadolivre.com.br", "", StringComparison.OrdinalIgnoreCase)
                .Trim()
                .ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
            if (!string.IsNullOrEmpty(slug))
                TryAdd(links, seen, "Mercado Livre (lista)", "https://lista.mercadolivre.com.br/" + slug);
        }
    }

    private static void AddLinksFromDiagnostics(
        List<MarketPriceReferenceLink> links,
        HashSet<string> seen,
        IReadOnlyList<BenchmarkDiagnosticEntry>? diagnostics)
    {
        if (diagnostics is not { Count: > 0 })
            return;

        foreach (var d in diagnostics)
        {
            if (string.IsNullOrWhiteSpace(d.Detail))
                continue;

            if (TopResultsPrefix.IsMatch(d.Detail))
            {
                var payload = d.Detail["top_results=".Length..];
                foreach (var part in payload.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var sep = part.IndexOf('|');
                    if (sep <= 0)
                        continue;
                    var url = part[..sep].Trim();
                    var title = part[(sep + 1)..].Trim();
                    if (string.IsNullOrEmpty(url))
                        continue;
                    var label = string.IsNullOrEmpty(title) ? "Resultado web" : Truncate(title, 72);
                    TryAdd(links, seen, label, url);
                }

                continue;
            }

            foreach (Match m in UrlRegex.Matches(d.Detail))
            {
                var url = m.Value.TrimEnd('.', ',', ';');
                TryAdd(links, seen, "Referência citada", url);
            }
        }
    }

    private static void TryAdd(List<MarketPriceReferenceLink> links, HashSet<string> seen, string label, string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
            return;

        links.Add(new MarketPriceReferenceLink { Label = label.Trim(), Url = url.Trim() });
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
