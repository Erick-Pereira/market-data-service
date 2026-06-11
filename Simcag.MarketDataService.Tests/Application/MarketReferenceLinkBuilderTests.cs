using Simcag.MarketDataService.Application.Benchmarking;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class MarketReferenceLinkBuilderTests
{
    [Fact]
    public void Build_inclui_links_de_reproducao_e_top_results()
    {
        var diagnostics = new List<BenchmarkDiagnosticEntry>
        {
            new(
                "provider:searxng",
                "fetch",
                "ok",
                "top_results=https://loja.test/nvr|NVR 16 canais;https://loja.test/b|Outro"),
        };

        var links = MarketReferenceLinkBuilder.Build(
            "NVR preço site:mercadolivre.com.br",
            diagnostics,
            "http://localhost:8088");

        Assert.Contains(links, l => l.Label.Contains("Google", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(links, l => l.Url.Contains("lista.mercadolivre.com.br", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(links, l => l.Url.Contains("localhost:8088/search", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(links, l => l.Url == "https://loja.test/nvr");
    }
}
