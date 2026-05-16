using Simcag.MarketDataService.Application.Services;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class BrazilianMoneyParserTests
{
    [Fact]
    public void ExtractAll_strips_nbsp_and_finds_brl()
    {
        var text = "Produto custa R$\u00A01.234,56 em promoção.";
        var amounts = BrazilianMoneyParser.ExtractAll(text);
        Assert.Contains(1234.56m, amounts);
    }

    [Fact]
    public void Median_orders_values()
    {
        var m = BrazilianMoneyParser.Median(new[] { 10m, 1m, 5m });
        Assert.Equal(5m, m);
    }
}
