using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Services;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class SearxngJsonParserTests
{
    [Fact]
    public void ExtractResultText_agrega_titulos_e_conteudo()
    {
        const string json = """
            {
              "results": [
                { "title": "Câmera IP 2MP", "content": "Por R$ 189,90 no Mercado Livre" },
                { "title": "Kit CFTV", "content": "A partir de R$ 299,00" }
              ]
            }
            """;

        var text = SearxngJsonParser.ExtractResultText(json);
        var amounts = BrazilianMoneyParser.ExtractAll(text);

        Assert.Contains(189.90m, amounts);
        Assert.Contains(299.00m, amounts);
    }

    [Fact]
    public void ExtractResultText_json_invalido_retorna_vazio()
    {
        Assert.Equal(string.Empty, SearxngJsonParser.ExtractResultText("{ not json"));
    }

    [Fact]
    public void ExtractTopResults_retorna_urls_e_titulos()
    {
        const string json = """
            {
              "results": [
                { "url": "https://produto.example/a", "title": "NVR 16 canais", "content": "R$ 2200" },
                { "url": "https://produto.example/b", "title": "Kit IP", "content": "R$ 1999" }
              ]
            }
            """;

        var links = SearxngJsonParser.ExtractTopResults(json, 5);
        Assert.Equal(2, links.Count);
        Assert.Equal("https://produto.example/a", links[0].Url);
        Assert.Contains("NVR", links[0].Title);
    }

    [Fact]
    public void ExtractResultPriceSamples_associa_preco_por_url()
    {
        const string json = """
            {
              "results": [
                { "url": "https://mercadolivre.com.br/camera", "title": "Câmera IP 2MP", "content": "Por R$ 890,00" },
                { "url": "https://amazon.com.br/camera", "title": "Câmera dome", "content": "R$ 1.299,90" }
              ]
            }
            """;

        var samples = SearxngJsonParser.ExtractResultPriceSamples(json, 5);
        Assert.Equal(2, samples.Count);
        Assert.Equal(890m, samples[0].PriceBrl);
        Assert.Equal("https://mercadolivre.com.br/camera", samples[0].Url);
        Assert.Equal("searxng", samples[0].Provider);
    }

    [Fact]
    public void ExtractResultPriceSamples_inclui_link_sem_preco_confirmado()
    {
        const string json = """
            {
              "results": [
                { "url": "https://loja.example/produto", "title": "NVR genérico", "content": "Frete grátis para todo Brasil" },
                { "url": "https://mercadolivre.com.br/nvr", "title": "NVR 16 canais", "content": "R$ 2.199,00" }
              ]
            }
            """;

        var samples = SearxngJsonParser.ExtractResultPriceSamples(json, 5);
        Assert.Equal(2, samples.Count);
        Assert.Null(samples[0].PriceBrl);
        Assert.Equal("https://loja.example/produto", samples[0].Url);
        Assert.Equal(2199m, samples[1].PriceBrl);
    }

    [Fact]
    public void ExtractResultPriceSamples_ignores_installment_snippet()
    {
        const string json = """
            {
              "results": [
                {
                  "url": "https://mercadolivre.com.br/camera-vip",
                  "title": "Câmera Ip Intelbras Vip 5232",
                  "content": "R$7.636,88 R$6.720,45 12% OFF 12x R$645,95"
                }
              ]
            }
            """;

        var samples = SearxngJsonParser.ExtractResultPriceSamples(json, 5);
        Assert.Single(samples);
        Assert.Equal(6720.45m, samples[0].PriceBrl);
    }
}
