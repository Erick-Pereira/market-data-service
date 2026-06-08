namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>Valores estáveis de estado de benchmark (API / logs / métricas).</summary>
public static class BenchmarkStatuses
{
    public const string ResolvedExternal = "resolved_external";
    public const string DocumentAnchor = "document_anchor";
    public const string CacheHit = "cache_hit";
    public const string DatabaseHit = "database_hit";
    public const string RejectedSpread = "rejected_spread";
    public const string RejectedInsufficientSamples = "rejected_insufficient_samples";
    public const string RejectedDistinctSamples = "rejected_distinct_samples_policy";
    public const string RejectedDeclaredMismatch = "rejected_declared_mismatch";
    public const string Empty = "empty";
}
