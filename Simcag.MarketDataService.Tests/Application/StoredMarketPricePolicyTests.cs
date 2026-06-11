using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Domain.Entities;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class StoredMarketPricePolicyTests
{
    [Fact]
    public void ShouldRefresh_WhenStoredIsDocumentAnchor()
    {
        var stored = MarketPrice.CreateObservation("Camera IP", 100m, "DocumentDeclaredReference", "Outros", "BR-Nacional");
        Assert.True(StoredMarketPricePolicy.ShouldRefresh(890m, stored));
    }

    [Fact]
    public void ShouldNotRefresh_WhenDocumentAnchorMatchesDeclared()
    {
        var stored = MarketPrice.CreateObservation(
            "Automacao predial",
            1_500_000m,
            "DocumentDeclaredReference",
            "Outros",
            "BR-Nacional");
        Assert.False(StoredMarketPricePolicy.ShouldRefresh(1_500_000m, stored));
    }

    [Fact]
    public void ShouldRefresh_WhenDeclaredDiffersMoreThan40Percent()
    {
        var stored = MarketPrice.CreateObservation("Camera IP", 100m, "WebScrape:Aggregated", "Outros", "BR-Nacional");
        Assert.True(StoredMarketPricePolicy.ShouldRefresh(890m, stored));
    }

    [Fact]
    public void ShouldNotRefresh_WhenStoredMatchesDeclared()
    {
        var stored = MarketPrice.CreateObservation("Material", 3500m, "PostgreSQL", "Outros", "BR-Nacional");
        Assert.False(StoredMarketPricePolicy.ShouldRefresh(3500m, stored));
    }

    [Fact]
    public void IsExternalBenchmarkSource_ExcludesDocumentAnchor()
    {
        Assert.False(StoredMarketPricePolicy.IsExternalBenchmarkSource("DocumentDeclaredReference"));
        Assert.True(StoredMarketPricePolicy.IsExternalBenchmarkSource("WebScrape:Aggregated"));
    }
}
