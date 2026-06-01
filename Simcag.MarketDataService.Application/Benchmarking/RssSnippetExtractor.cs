using System.Text.RegularExpressions;

namespace Simcag.MarketDataService.Application.Benchmarking;

internal static class RssSnippetExtractor
{
    private static readonly Regex ItemDescriptionRx = new(
        @"<item>[\s\S]*?<description>(?<d>[\s\S]*?)</description>[\s\S]*?</item>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string ExtractItemDescriptions(string? rssXml)
    {
        if (string.IsNullOrWhiteSpace(rssXml))
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (Match m in ItemDescriptionRx.Matches(rssXml))
        {
            if (!m.Success)
                continue;
            var chunk = StripTags(m.Groups["d"].Value);
            if (chunk.Length > 0)
                sb.AppendLine(chunk);
        }

        return sb.ToString();
    }

    private static string StripTags(string input)
    {
        input = Regex.Replace(input, @"<!\[CDATA\[|\]\]>", "", RegexOptions.IgnoreCase);
        input = Regex.Replace(input, @"<[^>]+>", " ");
        return Regex.Replace(input, @"\s+", " ").Trim();
    }
}
