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

    [Fact]
    public void SelectListingPrice_ignores_installment_and_prefers_sale_price()
    {
        const string text = "R$7.636,88 R$6.720,45 12% OFF 12x R$645,95";
        var price = BrazilianMoneyParser.SelectListingPrice(text);
        Assert.Equal(6720.45m, price);
    }

    [Fact]
    public void SelectListingPrice_returns_null_when_only_installment()
    {
        const string text = "Em até 12x R$645,95 sem juros";
        var price = BrazilianMoneyParser.SelectListingPrice(text);
        Assert.Null(price);
    }

    [Fact]
    public void SelectListingPrice_returns_null_when_title_has_12x_far_from_price()
    {
        const string text =
            "Camera IP Full HD 2MP Em Até 12x sem juros frete grátis referência R$645,95";
        var price = BrazilianMoneyParser.SelectListingPrice(text);
        Assert.Null(price);
    }

    [Fact]
    public void ExtractListingPrices_excludes_installment_amounts()
    {
        const string text = "R$7.636,88 R$6.720,45 12x R$645,95";
        var amounts = BrazilianMoneyParser.ExtractListingPrices(text);
        Assert.Contains(6720.45m, amounts);
        Assert.Contains(7636.88m, amounts);
        Assert.DoesNotContain(645.95m, amounts);
    }
}
