using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Moq;

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
    [InlineData(694, 4200, false)]
    [InlineData(554, 4200, false)]
    [InlineData(2200, 4200, true)]
    public void IsPlausible_valida_faixa(decimal candidate, decimal declared, bool expected) =>
        Assert.Equal(expected, DeclaredReferencePlausibility.IsPlausible(candidate, declared));

    [Fact]
    public void FilterSamples_high_value_rejeita_produtos_baratos_incompativeis()
    {
        var filtered = DeclaredReferencePlausibility.FilterSamples([554m, 694m, 499m, 2200m], 4200m);
        Assert.Equal([2200m], filtered);
    }

    [Fact]
    public void ResolveMinRatio_escala_com_valor_declarado()
    {
        Assert.Equal(0.05m, DeclaredReferencePlausibility.ResolveMinRatio(100m));
        Assert.Equal(0.15m, DeclaredReferencePlausibility.ResolveMinRatio(890m));
        Assert.Equal(0.20m, DeclaredReferencePlausibility.ResolveMinRatio(4200m));
    }

    [Fact]
    public void FilterSamplesTightBand_remove_outliers_espurios()
    {
        var declared = 890m;
        var samples = new[] { 89m, 185m, 750m, 890m, 1200m, 3500m };
        var filtered = DeclaredReferencePlausibility.FilterSamplesTightBand(samples, declared);
        Assert.Equal([750m, 890m, 1200m], filtered);
    }
}

public sealed class HistoricalPriceBenchmarkResolverTests
{
    [Fact]
    public async Task TryResolveAsync_uses_median_of_history_excluding_curated()
    {
        var repo = new Mock<IMarketPriceHistoryRepository>();
        repo.Setup(r => r.GetByProductNameAsync("Camera IP", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                MarketPriceHistory.Create("Camera IP", 180m, "WebScrape:Aggregated", DateTime.UtcNow.AddDays(-3)),
                MarketPriceHistory.Create("Camera IP", 200m, "WebScrape:Aggregated", DateTime.UtcNow.AddDays(-1)),
                MarketPriceHistory.Create("Camera IP", 185m, "CuratedCategoryBenchmark:camera", DateTime.UtcNow),
            ]);

        var result = await HistoricalPriceBenchmarkResolver.TryResolveAsync(
            repo.Object,
            ["Camera IP"],
            190m,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(200m, result!.Price);
        Assert.Equal(HistoricalPriceBenchmarkResolver.SourcePrefix, result.Source);
    }
}
