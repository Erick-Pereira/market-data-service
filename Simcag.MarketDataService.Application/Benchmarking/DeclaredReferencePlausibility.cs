namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Rejeita amostras de scraping incompatíveis com o valor declarado na linha (ex.: R$ 4,93 vs R$ 3.500).
/// </summary>
public static class DeclaredReferencePlausibility
{
    /// <summary>Mínimo mediana/amostra vs declarado para itens de baixo valor (5%).</summary>
    public const decimal MinRatioToDeclared = 0.05m;

    /// <summary>Máximo mediana/amostra vs declarado (500%).</summary>
    public const decimal MaxRatioToDeclared = 5.0m;

    /// <summary>Faixa mínima mais estrita para itens ≥ R$ 500 (15%).</summary>
    public const decimal MinRatioMidValue = 0.15m;

    /// <summary>Faixa mínima mais estrita para itens ≥ R$ 2.000 (20%).</summary>
    public const decimal MinRatioHighValue = 0.20m;

    public static decimal ResolveMinRatio(decimal declaredReferenceBrl)
    {
        if (declaredReferenceBrl >= 2000m)
            return MinRatioHighValue;
        if (declaredReferenceBrl >= 500m)
            return MinRatioMidValue;
        return MinRatioToDeclared;
    }

    public static bool IsPlausible(decimal candidatePrice, decimal declaredReferenceBrl)
    {
        if (declaredReferenceBrl <= 0.01m || candidatePrice <= 0.01m)
            return true;

        var ratio = candidatePrice / declaredReferenceBrl;
        var minRatio = ResolveMinRatio(declaredReferenceBrl);
        return ratio >= minRatio && ratio <= MaxRatioToDeclared;
    }

    public static IReadOnlyList<decimal> FilterSamples(
        IReadOnlyList<decimal> samples,
        decimal? declaredReferenceBrl)
    {
        if (declaredReferenceBrl is not > 0.01m || samples.Count == 0)
            return samples;

        var minRatio = ResolveMinRatio(declaredReferenceBrl.Value);
        var min = declaredReferenceBrl.Value * minRatio;
        var max = declaredReferenceBrl.Value * MaxRatioToDeclared;
        return samples.Where(s => s >= min && s <= max).ToList();
    }

    /// <summary>
    /// Faixa mais estreita (35%–250% do declarado) para reduzir dispersão de snippets espúrios
    /// quando a mediana bruta falha o limite de spread.
    /// </summary>
    public static IReadOnlyList<decimal> FilterSamplesTightBand(
        IReadOnlyList<decimal> samples,
        decimal declaredReferenceBrl,
        decimal minRatio = 0.35m,
        decimal maxRatio = 2.5m)
    {
        if (samples.Count == 0 || declaredReferenceBrl <= 0.01m)
            return samples;

        var min = declaredReferenceBrl * minRatio;
        var max = declaredReferenceBrl * maxRatio;
        return samples.Where(s => s >= min && s <= max).ToList();
    }
}
