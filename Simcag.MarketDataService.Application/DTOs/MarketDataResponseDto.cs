namespace Simcag.MarketDataService.Application.DTOs;

/// <summary>Contract aligned with condominium market benchmarking consumers.</summary>
public sealed class MarketDataResponseDto
{
    public required string Category { get; init; }
    public required string Region { get; init; }
    public decimal AveragePrice { get; init; }
    public decimal MedianPrice { get; init; }
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public int SampleSize { get; init; }
    /// <summary>Nulo quando não há amostras reais na base para o par categoria × região.</summary>
    public DateTime? LastUpdated { get; init; }
}
