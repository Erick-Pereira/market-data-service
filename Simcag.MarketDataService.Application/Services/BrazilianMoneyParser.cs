using System.Globalization;
using System.Text.RegularExpressions;

namespace Simcag.MarketDataService.Application.Services;

/// <summary>Extrai valores em Real (pt-BR) de texto livre (snippets de busca, HTML).</summary>
internal static partial class BrazilianMoneyParser
{
    /// <summary>Padrões típicos: R$ 1.234,56 / R$ 1234,56.</summary>
    private static readonly Regex[] Patterns =
    [
        new Regex(@"R\$\s*([\d]{1,3}(?:\.\d{3})*(?:,\d{2}))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new Regex(@"R\$\s*(\d+,\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        // Snippets BR frequentes sem "R$" explícito (ex.: "por 1.299,00")
        new Regex(@"(?<![\d/])(\d{1,3}(?:\.\d{3})+,\d{2})\b(?!\d)", RegexOptions.CultureInvariant),
    ];

    public static IReadOnlyList<decimal> ExtractAll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<decimal>();

        text = text.Replace('\u00A0', ' ').Replace('\u202F', ' ');

        var list = new List<decimal>();
        foreach (var rx in Patterns)
        {
            foreach (Match m in rx.Matches(text))
            {
                if (!m.Success || m.Groups.Count < 2)
                    continue;
                var raw = m.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal);
                if (TryParseBrazilianMoney(raw, out var amt) && IsPlausibleMarketPrice(amt))
                    list.Add(amt);
            }
        }

        return list;
    }

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
            return null;
        var s = values.OrderBy(x => x).ToArray();
        var mid = s.Length / 2;
        return s.Length % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2m;
    }

    /// <summary>Dispersão (max−min)/mediana; 0 se menos de 2 amostras ou mediana ≤ 0.</summary>
    public static decimal RelativeSpreadAroundMedian(IReadOnlyList<decimal> values, decimal median)
    {
        if (values.Count < 2 || median <= 0m)
            return 0m;
        var min = values.Min();
        var max = values.Max();
        return (max - min) / median;
    }

    /// <summary>Rejeita valores absurdos para referência de mercado.</summary>
    private static bool IsPlausibleMarketPrice(decimal amount) =>
        amount is >= 0.01m and <= 50_000_000m;

    private static bool TryParseBrazilianMoney(string raw, out decimal amount)
    {
        amount = 0;
        raw = raw.Trim();
        if (string.IsNullOrEmpty(raw))
            return false;

        var hasComma = raw.Contains(',');
        var hasDot = raw.Contains('.');
        string normalized;
        if (hasComma && hasDot)
            normalized = raw.Replace(".", "", StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal);
        else if (hasComma && !hasDot)
            normalized = raw.Replace(",", ".", StringComparison.Ordinal);
        else if (!hasComma && hasDot)
        {
            var parts = raw.Split('.');
            if (parts.Length > 1 && parts[^1].Length == 3 && parts[^1].All(char.IsDigit))
                normalized = raw.Replace(".", "", StringComparison.Ordinal);
            else
                normalized = raw;
        }
        else
            normalized = raw;

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
               && amount > 0m;
    }
}
