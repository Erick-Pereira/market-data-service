namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Heurísticas leves para HTML de motores de busca (não substituem browser real).
/// Sinaliza possível bloqueio para métricas e redução de confiança.
/// </summary>
internal static class ScrapeAntiBotHeuristics
{
    private static readonly string[] Signals =
    [
        "captcha", "recaptcha", "hcaptcha", "cf-browser-verification", "challenge-platform",
        "unusual traffic", "tráfego incomum", "verify you are human", "verificar que você é humano",
        "automated queries", "consultas automatizadas", "access denied", "acesso negado",
        "enable javascript", "habilitar javascript", "checking your browser", "ray id"
    ];

    public static bool LikelyBlocked(string? html, out string? matched)
    {
        matched = null;
        if (string.IsNullOrWhiteSpace(html))
            return false;

        foreach (var s in Signals)
        {
            if (ContainsOrdinalIgnoreCase(html, s))
            {
                matched = s;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOrdinalIgnoreCase(string haystack, string needle)
    {
        return haystack.AsSpan().IndexOf(needle.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
