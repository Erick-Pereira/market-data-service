namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Separação semântica: estimativa de mercado obtida fora do documento vs. âncora declarada no documento.
/// Não misturar — o <see cref="BenchmarkPriceKind"/> é o campo autoritativo para relatórios e API.
/// </summary>
public enum BenchmarkPriceKind
{
    /// <summary>Preço inferido a partir de fontes externas (web / SerpAPI).</summary>
    ExternalMarketEstimate = 0,

    /// <summary>Valor da própria linha/documento usado apenas como referência quando não há estimativa externa.</summary>
    DocumentAnchorPrice = 1
}
