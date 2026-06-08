using System.Text;
using System.Text.Json;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Extrai título + snippet dos resultados JSON do SearXNG.</summary>
internal static class SearxngJsonParser
{
    public static string ExtractResultText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var item in results.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    sb.Append(title.GetString()).Append(' ');
                if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                    sb.Append(content.GetString()).Append(' ');
            }

            return sb.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
