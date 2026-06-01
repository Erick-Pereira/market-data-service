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

    /// <summary>
    /// Bing RSS (XML): snippets orgânicos com preços em BRL quando o HTML estático não traz resultados.
    /// </summary>
    public bool EnableBingRssScrape { get; set; } = true;

    /// <summary>Máximo de segundos por pedido HTTP de pesquisa.</summary>
    public int HttpTimeoutSeconds { get; set; } = 25;

    /// <summary>
    /// Mínimo de menções a valores em BRL extraídas do HTML para aceitar mediana (scraping DDG/Bing agregado).
    /// Default 1: com agregação DDG+Bing e valores <c>R$</c> + formato BR, uma amostra plausível já alimenta o benchmark.
    /// </summary>
    public int MinimumPriceSamplesForWebScrape { get; set; } = 1;

    /// <summary>Mínimo de preços estruturados (ex.: Serp shopping) para aceitar mediana.</summary>
    public int MinimumPriceSamplesForStructuredList { get; set; } = 1;

    /// <summary>
    /// Se (max−min)/mediana do conjunto extraído exceder este valor, rejeita-se a cotação de scraping (HTML).
    /// SerpAPI shopping ignora este limite (lista já mais estruturada).
    /// </summary>
    public decimal MaxRelativeSpreadForWebScrape { get; set; } = 2.5m;

    /// <summary>Retentativas adicionais por pedido HTTP de scrape (total tentativas = 1 + valor).</summary>
    public int ScrapeMaxRetries { get; set; } = 2;

    /// <summary>Backoff base entre retentativas de scrape (ms).</summary>
    public int ScrapeRetryBaseDelayMilliseconds { get; set; } = 400;

    /// <summary>
    /// Quando verdadeiro, exige pelo menos duas amostras distintas de scraping HTML antes de mediana
    /// (reduz falsos positivos de um único valor espúrio). SerpAPI shopping não é afetado.
    /// </summary>
    public bool RequireDistinctSamplesForWebScrape { get; set; }
}
