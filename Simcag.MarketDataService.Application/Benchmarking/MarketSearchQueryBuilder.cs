using System.Text.RegularExpressions;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Monta consultas de busca para scraping — corrige typos comuns e gera variantes curtas para serviços B2B.</summary>
internal static class MarketSearchQueryBuilder
{
    private static readonly (string Pattern, string Replacement)[] TypoFixes =
    [
        ("Correktiva", "Corretiva"),
        ("Preventtiva", "Preventiva"),
    ];

    public static IReadOnlyList<string> BuildVariants(string rawProductName)
    {
        var normalized = FixTypos(rawProductName.Trim());
        if (string.IsNullOrWhiteSpace(normalized))
            return Array.Empty<string>();

        var list = new List<string>();
        void Add(string q)
        {
            q = q.Trim();
            if (q.Length < 4)
                return;
            if (!list.Contains(q, StringComparer.OrdinalIgnoreCase))
                list.Add(q);
        }

        Add(BuildPrimaryQuery(normalized));

        var compact = CompactForSearch(normalized);
        if (!string.Equals(compact, normalized, StringComparison.OrdinalIgnoreCase))
            Add(BuildPrimaryQuery(compact));

        foreach (var fallback in InferCategoryFallbackQueries(normalized))
            Add(fallback);

        return list;
    }

    private static string FixTypos(string text)
    {
        foreach (var (pattern, replacement) in TypoFixes)
            text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
        return text;
    }

    private static string BuildPrimaryQuery(string raw) =>
        raw.Contains("preço", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("preco", StringComparison.OrdinalIgnoreCase)
            ? raw + " Brasil"
            : raw + " preço Brasil";

    /// <summary>Reduz descrições longas de NF-e/serviço a termos pesquisáveis.</summary>
    private static string CompactForSearch(string text)
    {
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= 48)
            return text;

        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "da", "do", "das", "dos", "e", "em", "para", "por", "com", "a", "o", "as", "os",
            "prestação", "prestacao", "serviços", "servicos", "serviço", "servico",
        };

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stop.Contains(w))
            .Take(6)
            .ToArray();

        return words.Length >= 2 ? string.Join(' ', words) : text;
    }

    private static IEnumerable<string> InferCategoryFallbackQueries(string text)
    {
        var u = text.ToUpperInvariant();
        if (u.Contains("ELEVADOR", StringComparison.Ordinal))
            yield return "manutenção elevador condomínio preço mensal Brasil";
        if (u.Contains("LIMPEZA", StringComparison.Ordinal) || u.Contains("HIGIEN", StringComparison.Ordinal))
            yield return "serviço limpeza condomínio preço mensal Brasil";
        if (u.Contains("SEGURAN", StringComparison.Ordinal) || u.Contains("PORTARIA", StringComparison.Ordinal))
            yield return "serviço portaria condomínio preço mensal Brasil";
        if (u.Contains("CAMERA", StringComparison.Ordinal) || u.Contains("CÂMERA", StringComparison.Ordinal)
            || u.Contains("CFTV", StringComparison.Ordinal))
            yield return "câmera IP 2MP preço Brasil mercado livre";
        if (u.Contains("NVR", StringComparison.Ordinal))
            yield return "NVR 16 canais IP preço Brasil";
        if (u.Contains("DETERGENTE", StringComparison.Ordinal))
            yield return "detergente 5 litros preço Brasil";
        if (LooksLikeRetailProduct(text))
            yield return CompactForSearch(text) + " preço site:mercadolivre.com.br";
    }

    private static bool LooksLikeRetailProduct(string text)
    {
        var u = text.ToUpperInvariant();
        return u.Contains("CAMERA", StringComparison.Ordinal) || u.Contains("NVR", StringComparison.Ordinal)
               || u.Contains("SWITCH", StringComparison.Ordinal) || u.Contains("ROTEADOR", StringComparison.Ordinal)
               || u.Contains("HD EXTERNO", StringComparison.Ordinal) || u.Contains("SSD", StringComparison.Ordinal);
    }
}
