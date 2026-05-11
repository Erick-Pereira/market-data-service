namespace Simcag.MarketDataService.Application.Interfaces;

/// <summary>
/// Obtém um preço de referência a partir de pesquisa na web (API indexada ou scraping permitido).
/// Devolve <c>null</c> quando não há dados reais utilizáveis — nunca inventa valores.
/// </summary>
public interface IMarketPriceResearchService
{
    /// <param name="productQuery">Termo de busca (ex.: descrição da linha de despesa).</param>
    /// <returns>Preço mediano/plausível extraído de resultados reais, ou null.</returns>
    Task<MarketPriceResearchResult?> TryResolvePriceAsync(string productQuery, CancellationToken cancellationToken = default);
}

/// <param name="Price">Valor monetário positivo obtido de fonte externa.</param>
/// <param name="Source">Identificador da fonte (ex. SerpApi:GoogleShopping).</param>
/// <param name="EvidenceSnippet">Trecho ou resumo onde o preço apareceu (auditoria).</param>
public sealed record MarketPriceResearchResult(decimal Price, string Source, string? EvidenceSnippet);
