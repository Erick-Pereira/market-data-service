using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Interfaces;

public interface IMarketDataService
{
    /// <param name="declaredReferenceBrl">Valor declarado na linha (ex.: extrato); usado só se a pesquisa web não devolver cotação, para persistir referência auditável.</param>
    Task<MarketPrice?> GetPriceAsync(string productName, CancellationToken ct, decimal? declaredReferenceBrl = null);

    /// <summary>Inclui metadados de amostragem, dispersão e confiança heurística para a API.</summary>
    Task<MarketPriceResolution?> ResolvePriceAsync(string productName, CancellationToken ct, decimal? declaredReferenceBrl = null);

    Task<IEnumerable<MarketPriceHistory>> GetPriceHistoryAsync(string productName, int days, CancellationToken ct);

    /// <summary>
    /// Atualiza benchmark de preço para produto já cadastrado no sistema.
    /// Usado por cron jobs ou triggers do processing-service.
    /// </summary>
    Task<MarketPriceResolution?> UpdateBenchmarkForExistingProductAsync(Guid productId, string productName, CancellationToken ct);
}
