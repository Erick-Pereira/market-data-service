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

    private static readonly Regex InstallmentBeforePrice = new(
        @"(?<!\d)(\d{1,2})\s*x\s*(?:de\s*)?(?:R\$|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InstallmentEmAte = new(
        @"em\s+(?:até\s+|ate\s+)?(\d{1,2})\s*x\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InstallmentAtéNxDe = new(
        @"(?:até|ate)\s+(\d{1,2})\s*x\s*de\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InstallmentNxSuffix = new(
        @"\b(\d{1,2})\s*x\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<decimal> ExtractAll(string? text) =>
        ExtractListingPrices(text, includeInstallments: true);

    /// <summary>Valores em BRL excluindo parcelas (12x R$ …) quando possível.</summary>
    public static IReadOnlyList<decimal> ExtractListingPrices(string? text, bool includeInstallments = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<decimal>();

        text = NormalizeText(text);
        var located = ExtractAllLocated(text);
        if (located.Count == 0)
            return Array.Empty<decimal>();

        var amounts = includeInstallments
            ? located.Select(p => p.Amount).ToList()
            : located.Where(p => !IsInstallmentPrice(text, p)).Select(p => p.Amount).ToList();

        return amounts;
    }

    /// <summary>Escolhe o preço à vista mais provável em snippet de e-commerce (evita parcela).</summary>
    public static decimal? SelectListingPrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = NormalizeText(text);
        var listing = ExtractListingPrices(text);
        if (listing.Count == 0)
            return null;

        if (listing.Count == 1)
        {
            var located = ExtractAllLocated(normalized);
            if (located.Count == 1 && IsInstallmentPrice(normalized, located[0]))
                return null;

            if (SnippetLooksInstallmentOnly(normalized, located))
                return null;

            return listing[0];
        }

        var max = listing.Max();
        var floor = max * 0.15m;
        var tier = listing.Where(p => p >= floor).OrderBy(p => p).ToList();
        if (tier.Count == 0)
            return listing.Min();

        if (tier.Count >= 2 && tier[^1] / tier[0] <= 1.35m)
            return tier[0];

        return tier.Count >= 2 ? tier[^1] : tier[0];
    }

    private sealed record LocatedPrice(decimal Amount, int Index);

    private static List<LocatedPrice> ExtractAllLocated(string text)
    {
        var list = new List<LocatedPrice>();
        foreach (var rx in Patterns)
        {
            foreach (Match m in rx.Matches(text))
            {
                if (!m.Success || m.Groups.Count < 2)
                    continue;
                var raw = m.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal);
                if (TryParseBrazilianMoney(raw, out var amt) && IsPlausibleMarketPrice(amt))
                    list.Add(new LocatedPrice(amt, m.Groups[1].Index));
            }
        }

        return list
            .GroupBy(p => p.Amount)
            .Select(g => g.OrderBy(p => p.Index).First())
            .OrderBy(p => p.Index)
            .ToList();
    }

    private static bool IsInstallmentPrice(string text, LocatedPrice price)
    {
        var start = Math.Max(0, price.Index - 80);
        var end = Math.Min(text.Length, price.Index + 32);
        var window = text[start..end];
        var prefix = text[start..price.Index];

        if (InstallmentBeforePrice.IsMatch(prefix + "R$"))
            return true;

        if (InstallmentEmAte.IsMatch(window) && price.Amount < 5000m)
            return true;

        if (InstallmentAtéNxDe.IsMatch(window) && price.Amount < 5000m)
            return true;

        // Title/snippet may mention "12x" far from the value — link via proximity in full text.
        if (price.Amount < 5000m && HasInstallmentMarkerNearPrice(text, price.Index))
            return true;

        return false;
    }

    private static bool HasInstallmentMarkerNearPrice(string text, int priceIndex)
    {
        var start = Math.Max(0, priceIndex - 120);
        var end = Math.Min(text.Length, priceIndex + 40);
        var window = text[start..end];
        if (!InstallmentNxSuffix.IsMatch(window))
            return false;

        foreach (Match m in InstallmentNxSuffix.Matches(window))
        {
            var markerIndex = start + m.Index;
            if (Math.Abs(markerIndex - priceIndex) <= 96)
                return true;
        }

        return false;
    }

    private static bool SnippetLooksInstallmentOnly(string text, IReadOnlyList<LocatedPrice> located)
    {
        if (located.Count != 1)
            return false;

        var hasInstallmentMarker =
            InstallmentEmAte.IsMatch(text)
            || InstallmentAtéNxDe.IsMatch(text)
            || InstallmentBeforePrice.IsMatch(text);

        return hasInstallmentMarker && located[0].Amount < 5000m;
    }

    private static string NormalizeText(string text) =>
        text.Replace('\u00A0', ' ').Replace('\u202F', ' ');

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
