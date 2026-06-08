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
}
