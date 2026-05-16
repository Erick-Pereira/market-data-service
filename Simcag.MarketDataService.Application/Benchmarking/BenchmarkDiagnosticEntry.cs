namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Evidência estruturada para auditoria de uma etapa da pipeline de benchmark.</summary>
public sealed record BenchmarkDiagnosticEntry(
    string Scope,
    string Phase,
    string Message,
    string? Detail = null);
