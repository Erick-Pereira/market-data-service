using Simcag.MarketDataService.Domain.Aggregation;
using Simcag.MarketDataService.Domain.ValueObjects;

namespace Simcag.MarketDataService.Tests.Domain;

public sealed class MarketPriceAggregationTests
{
    [Fact]
    public void BuildSnapshot_Computes_expected_stats_for_simple_set()
    {
        var prices = new decimal[] { 10, 12, 11, 13, 12 };
        var cat = ExpenseCategory.FromInput("Hardware");
        var reg = GeographicRegion.FromInput("BR-Nacional");

        var snap = MarketPriceAggregation.BuildSnapshot(cat, reg, prices, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Hardware", snap.Category);
        Assert.Equal("BR-Nacional", snap.Region);
        Assert.Equal(5, snap.SampleSize);
        Assert.True(snap.AveragePrice > 0);
        Assert.True(snap.MedianPrice > 0);
        Assert.True(snap.MinPrice <= snap.MaxPrice);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), snap.LastUpdatedUtc);
    }

    [Fact]
    public void RemoveOutliersIqr_trims_extreme_values_when_enough_samples()
    {
        var prices = new decimal[] { 10, 11, 12, 11, 10, 1000 };
        var trimmed = MarketPriceAggregation.RemoveOutliersIqr(prices);
        Assert.DoesNotContain(1000m, trimmed);
    }
}
