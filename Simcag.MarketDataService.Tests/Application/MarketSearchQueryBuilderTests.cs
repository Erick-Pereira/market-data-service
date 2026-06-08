using Simcag.MarketDataService.Application.Benchmarking;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class MarketSearchQueryBuilderTests
{
    [Fact]
    public void BuildVariants_corrige_typo_e_adiciona_fallback_elevador()
    {
        var variants = MarketSearchQueryBuilder.BuildVariants(
            "Manutenção Preventiva e Correktiva de Elevadores");

        Assert.Contains(variants, v => v.Contains("Corretiva", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(variants, v => v.Contains("elevador", StringComparison.OrdinalIgnoreCase)
                                       && v.Contains("preço", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildVariants_inclui_fallback_camera_e_mercado_livre()
    {
        var variants = MarketSearchQueryBuilder.BuildVariants("Camera IP Full HD 2MP bullet");

        Assert.Contains(variants, v => v.Contains("câmera IP", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(variants, v => v.Contains("mercadolivre.com.br", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RssSnippetExtractor_encontra_preco_em_item()
    {
        const string rss = """
            <?xml version="1.0"?><rss><channel><title>x</title>
            <item><title>Loja</title><description>SABAO R$ 1,93 promo</description></item>
            </channel></rss>
            """;

        var text = RssSnippetExtractor.ExtractItemDescriptions(rss);
        var amounts = Simcag.MarketDataService.Application.Services.BrazilianMoneyParser.ExtractAll(text);

        Assert.Contains(1.93m, amounts);
    }
}
