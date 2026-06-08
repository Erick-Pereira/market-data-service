namespace Simcag.MarketDataService.Application.Configuration;

/// <summary>
/// Pesquisa de preços em fontes públicas na web (sem valores inventados).
/// Caminho padrão: SearXNG self-hosted (se configurado) + scraping HTML (DDG/Bing) e mediana de menções a R$.
/// SerpAPI é opt-in explícito (serviço pago) — desligado por defeito.
/// </summary>
public sealed class MarketResearchOptions
{
    /// <summary>
    /// URL base do SearXNG local (ex. http://localhost:8088). Vazio desliga o provider.
    /// </summary>
    public string? SearxngBaseUrl { get; set; } = "http://localhost:8088";

    /// <summary>Usa SearXNG JSON API como provider principal (requer instância acessível).</summary>
    public bool EnableSearxngScrape { get; set; } = true;

    /// <summary>Cooldown após connection refused antes de voltar a tentar SearXNG.</summary>
    public int SearxngUnavailableCooldownMinutes { get; set; } = 5;

    /// <summary>Timeout de ligação TCP ao SearXNG local (segundos).</summary>
    public int SearxngConnectTimeoutSeconds { get; set; } = 2;

    /// <summary>Chave SerpAPI — só usada se <see cref="EnableSerpApiFallback"/> estiver ativo.</summary>
    public string? SerpApiKey { get; set; }

    /// <summary>Fallback pago SerpAPI — requer MARKET_DATA__ENABLE_SERPAPI=true e chave configurada.</summary>
    public bool EnableSerpApiFallback { get; set; }

    /// <summary>Benchmarks curados por categoria (sem API externa) quando web falha ou é implausível.</summary>
    public bool EnableCuratedCategoryBenchmark { get; set; } = true;

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
