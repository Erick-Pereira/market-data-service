using Simcag.MarketDataService.Application.Interfaces;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Resultado completo da pesquisa web, incluindo rejeições explícitas para auditoria.</summary>
public sealed record MarketPriceResearchDetailedOutcome(
    MarketPriceResearchResult? Result,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<BenchmarkDiagnosticEntry> Diagnostics);
