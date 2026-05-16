using System.Text;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Simcag.Shared.Telemetry;

namespace Simcag.MarketDataService.Application.Benchmarking;

internal static class HtmlSnippetExtractors
{
    public static string DuckDuckGoLite(string html, ILogger log)
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
        catch (Exception ex)
        {
            SimcagMeters.MarketDataScrapeParseErrors.Add(1,
                new KeyValuePair<string, object?>("stage", "ddg_lite_dom"),
                new KeyValuePair<string, object?>("exception", ex.GetType().Name));
            log.LogWarning(ex, "Falha ao parsear HTML DuckDuckGo lite (fallback para texto bruto)");
        }

        return html;
    }

    public static string DuckDuckGoHtml(string html, ILogger log)
    {
        try
        {
            var doc = new HtmlParser().ParseDocument(html);
            var results = doc.QuerySelector(".results") ?? doc.QuerySelector("#links");
            if (results is not null)
                return results.TextContent ?? string.Empty;
        }
        catch (Exception ex)
        {
            SimcagMeters.MarketDataScrapeParseErrors.Add(1,
                new KeyValuePair<string, object?>("stage", "ddg_html_dom"),
                new KeyValuePair<string, object?>("exception", ex.GetType().Name));
            log.LogWarning(ex, "Falha ao parsear HTML DuckDuckGo html (fallback para texto bruto)");
        }

        return html;
    }

    public static string BingOrganicSnippets(string html, ILogger log)
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
        catch (Exception ex)
        {
            SimcagMeters.MarketDataScrapeParseErrors.Add(1,
                new KeyValuePair<string, object?>("stage", "bing_html_dom"),
                new KeyValuePair<string, object?>("exception", ex.GetType().Name));
            log.LogWarning(ex, "Falha ao parsear HTML Bing (resultado vazio)");
            return string.Empty;
        }
    }
}
