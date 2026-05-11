using Simcag.MarketDataService.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Interfaces;

public interface IMarketDataService
{
    /// <param name="declaredReferenceBrl">Valor declarado na linha (ex.: extrato); usado só se a pesquisa web não devolver cotação, para persistir referência auditável.</param>
    Task<MarketPrice?> GetPriceAsync(string productName, CancellationToken ct, decimal? declaredReferenceBrl = null);
    Task<IEnumerable<MarketPriceHistory>> GetPriceHistoryAsync(string productName, int days, CancellationToken ct);
}