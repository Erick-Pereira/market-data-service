using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.Shared.Contracts;

namespace Simcag.MarketDataService.Application.DTOs;

/// <summary>Resultado de resolução de preço com metadados para API e observabilidade (não é entidade de domínio).</summary>
public sealed record MarketPriceResolution(
    MarketPrice Quote,
    int? SampleCount,
    decimal? RelativeSpread,
    string? SearchQueryUsed,
    string NormalizedProductName,
    string Confidence,
    BenchmarkPriceKind BenchmarkKind = BenchmarkPriceKind.ExternalMarketEstimate,
    string BenchmarkStatus = BenchmarkStatuses.ResolvedExternal,
    decimal? ConfidenceScore = null,
    decimal? BenchmarkQualityScore = null,
    IReadOnlyList<BenchmarkDiagnosticEntry>? BenchmarkDiagnostics = null,
    IReadOnlyList<string>? BenchmarkRejectionTrail = null,
    IReadOnlyList<MarketPriceSample>? MarketSamples = null);
