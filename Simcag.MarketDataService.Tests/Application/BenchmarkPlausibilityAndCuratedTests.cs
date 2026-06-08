using Simcag.MarketDataService.Application.Benchmarking;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class DeclaredReferencePlausibilityTests
{
    [Fact]
    public void FilterSamples_remove_valores_incompativeis_com_declarado()
    {
        var filtered = DeclaredReferencePlausibility.FilterSamples([4.93m, 4.88m, 3500m], 3500m);
        Assert.Equal([3500m], filtered);
    }

    [Theory]
    [InlineData(4.93, 3500, false)]
    [InlineData(185, 890, true)]
    [InlineData(2000, 3500, true)]
    public void IsPlausible_valida_faixa(decimal candidate, decimal declared, bool expected) =>
        Assert.Equal(expected, DeclaredReferencePlausibility.IsPlausible(candidate, declared));
}

public sealed class CuratedCategoryBenchmarkCatalogTests
{
    [Fact]
    public void TryMatch_material_manutencao_predial()
    {
        var match = CuratedCategoryBenchmarkCatalog.TryMatch("Material de manutenção predial", 3500m);
        Assert.NotNull(match);
        Assert.Equal("manutencao_predial", match!.PatternId);
        Assert.Equal(2000m, match.ReferencePriceBrl);
    }

    [Fact]
    public void TryMatch_camera_ip_full_hd()
    {
        var match = CuratedCategoryBenchmarkCatalog.TryMatch("Camera IP Full HD 2MP", 890m);
        Assert.NotNull(match);
        Assert.Equal("camera_ip_2mp", match!.PatternId);
        Assert.Equal(185m, match.ReferencePriceBrl);
    }

    [Fact]
    public void TryMatch_rejeita_curado_incompativel_com_declarado()
    {
        var match = CuratedCategoryBenchmarkCatalog.TryMatch("Camera IP Full HD 2MP", 15m);
        Assert.Null(match);
    }
}
