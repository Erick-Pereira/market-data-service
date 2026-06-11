using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.Shared.Contracts;

namespace Simcag.MarketDataService.Application.Interfaces;

/// <summary>
/// Obtém um preço de referência a partir de pesquisa na web (API indexada ou scraping permitido).
/// Devolve <c>null</c> quando não há dados reais utilizáveis — nunca inventa valores.
/// </summary>
public interface IMarketPriceResearchService
{
    /// <param name="productQuery">Termo de busca (ex.: descrição da linha de despesa).</param>
    /// <returns>Preço mediano/plausível extraído de resultados reais, ou null.</returns>
    Task<MarketPriceResearchResult?> TryResolvePriceAsync(
        string productQuery,
        decimal? declaredReferenceBrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Igual a <see cref="TryResolvePriceAsync"/> mas inclui rejeições e diagnósticos para auditoria.
    /// </summary>
    Task<MarketPriceResearchDetailedOutcome> TryResolvePriceDetailedAsync(
        string productQuery,
        decimal? declaredReferenceBrl = null,
        CancellationToken cancellationToken = default);
}

/// <param name="Price">Valor monetário positivo obtido de fonte externa.</param>
/// <param name="Source">Identificador da fonte (ex. SerpApi:GoogleShopping) — legado / persistência.</param>
/// <param name="EvidenceSnippet">Trecho ou resumo onde o preço apareceu (auditoria).</param>
/// <param name="SampleCount">Número de valores em BRL usados antes da mediana (0 se não aplicável).</param>
/// <param name="RelativeSpread">(max−min)/mediana sobre as amostras (0 se &lt; 2 amostras).</param>
/// <param name="SearchQueryUsed">Consulta enviada ao motor de pesquisa (rastreabilidade).</param>
/// <param name="BenchmarkKind">Semântica: estimativa externa vs âncora documental (não usar só <see cref="Source"/>).</param>
/// <param name="BenchmarkStatus">Estado estável do benchmark (ver <see cref="BenchmarkStatuses"/>).</param>
/// <param name="ConfidenceScore">0–1 estimativa operacional de confiança no valor.</param>
/// <param name="BenchmarkQualityScore">0–1 qualidade metodológica (amostras + dispersão).</param>
/// <param name="Diagnostics">Evidência estruturada curta por etapa.</param>
public sealed record MarketPriceResearchResult(
    decimal Price,
    string Source,
    string? EvidenceSnippet,
    int SampleCount = 0,
    decimal RelativeSpread = 0,
    string? SearchQueryUsed = null,
    BenchmarkPriceKind BenchmarkKind = BenchmarkPriceKind.ExternalMarketEstimate,
    string BenchmarkStatus = BenchmarkStatuses.ResolvedExternal,
    decimal? ConfidenceScore = null,
    decimal? BenchmarkQualityScore = null,
    IReadOnlyList<BenchmarkDiagnosticEntry>? Diagnostics = null,
    IReadOnlyList<MarketPriceSample>? MarketSamples = null);
