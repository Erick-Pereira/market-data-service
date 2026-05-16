namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Pontuações 0–1 para auditoria: <see cref="Confidence"/> reflete confiança operacional no valor;
/// <see cref="Quality"/> reflete qualidade metodológica (amostras + dispersão).
/// </summary>
internal static class BenchmarkConfidenceCalculator
{
    public static (decimal Confidence, decimal Quality) Compute(
        int distinctSampleCount,
        decimal relativeSpread,
        bool structuredPriceList,
        bool antiBotLikely)
    {
        if (antiBotLikely)
            return (0.12m, 0.08m);

        // Qualidade: mais amostras distintas e menor dispersão aumentam o score.
        var q = 0.25m;
        q += Math.Min(distinctSampleCount, 12) * 0.055m;
        if (distinctSampleCount >= 2 && relativeSpread > 0m)
            q *= 1m / (1m + relativeSpread * 1.15m);
        if (structuredPriceList)
            q += 0.12m;
        q = Math.Clamp(q, 0m, 1m);

        // Confiança: penaliza dispersão alta mais agressivamente que qualidade.
        var c = structuredPriceList ? 0.48m : 0.32m;
        c += Math.Min(distinctSampleCount, 10) * 0.045m;
        if (distinctSampleCount >= 2 && relativeSpread > 0m)
            c *= 1m / (1m + relativeSpread * 1.4m);
        c = Math.Clamp(c, 0m, 1m);

        // Qualidade não deve ficar abaixo da confiança com muita frequência em listas estruturadas
        if (structuredPriceList && q < c)
            q = Math.Min(1m, c + 0.05m);

        return (c, q);
    }

    public static string LegacyConfidenceLabel(decimal confidence, BenchmarkPriceKind kind)
    {
        if (kind == BenchmarkPriceKind.DocumentAnchorPrice)
            return "document-anchor-only";

        if (confidence >= 0.62m)
            return "high";
        if (confidence >= 0.38m)
            return "medium";
        return "low";
    }
}
