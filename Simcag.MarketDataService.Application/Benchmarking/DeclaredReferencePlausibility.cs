namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Rejeita amostras de scraping incompatíveis com o valor declarado na linha (ex.: R$ 4,93 vs R$ 3.500).
/// </summary>
public static class DeclaredReferencePlausibility
{
    /// <summary>Mínimo mediana/amostra vs declarado (5%).</summary>
    public const decimal MinRatioToDeclared = 0.05m;

    /// <summary>Máximo mediana/amostra vs declarado (500%).</summary>
    public const decimal MaxRatioToDeclared = 5.0m;

    public static bool IsPlausible(decimal candidatePrice, decimal declaredReferenceBrl)
    {
        if (declaredReferenceBrl <= 0.01m || candidatePrice <= 0.01m)
            return true;

        var ratio = candidatePrice / declaredReferenceBrl;
        return ratio is >= MinRatioToDeclared and <= MaxRatioToDeclared;
    }

    public static IReadOnlyList<decimal> FilterSamples(
        IReadOnlyList<decimal> samples,
        decimal? declaredReferenceBrl)
    {
        if (declaredReferenceBrl is not > 0.01m || samples.Count == 0)
            return samples;

        var min = declaredReferenceBrl.Value * MinRatioToDeclared;
        var max = declaredReferenceBrl.Value * MaxRatioToDeclared;
        return samples.Where(s => s >= min && s <= max).ToList();
    }
}
