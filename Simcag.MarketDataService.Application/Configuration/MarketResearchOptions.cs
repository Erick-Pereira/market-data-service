namespace Simcag.MarketDataService.Application.Configuration;

/// <summary>
/// Pesquisa de preços em fontes públicas na web (sem valores inventados).
/// Caminho padrão: HTTP local para páginas HTML públicas (DuckDuckGo lite, Bing) e mediana de menções a R$.
/// SerpAPI é opcional e só é usado como último recurso se uma chave estiver configurada.
/// </summary>
public sealed class MarketResearchOptions
{
    /// <summary>Chave opcional SerpAPI — só usada se não vazia, após tentativas de scraping local.</summary>
    public string? SerpApiKey { get; set; }

    /// <summary>
    /// DuckDuckGo lite (HTML): extrai texto dos resultados e interpreta valores em BRL.
    /// </summary>
    public bool EnableDuckDuckGoLiteScrape { get; set; } = true;

    /// <summary>
    /// Bing (HTML de resultados orgânicos): complemento quando DDG não devolve amostras úteis.
    /// </summary>
    public bool EnableBingHtmlScrape { get; set; } = true;

    /// <summary>Máximo de segundos por pedido HTTP de pesquisa.</summary>
    public int HttpTimeoutSeconds { get; set; } = 25;
}
