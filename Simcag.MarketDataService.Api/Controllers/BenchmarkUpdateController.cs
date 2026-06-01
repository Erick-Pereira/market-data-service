using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Application.Interfaces;

namespace Simcag.MarketDataService.Api.Controllers;

/// <summary>
/// Controller para atualização periódica de benchmark de preços.
/// Usado por cron jobs ou triggers do processing-service para produtos já cadastrados no sistema.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BenchmarkUpdateController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;
    private readonly ILogger<BenchmarkUpdateController> _logger;

    public BenchmarkUpdateController(
        IMarketDataService marketDataService,
        ILogger<BenchmarkUpdateController> logger)
    {
        _marketDataService = marketDataService;
        _logger = logger;
    }

    /// <summary>
    /// Atualiza benchmark de preço para produto já cadastrado no sistema.
    /// Usado por cron jobs ou triggers do processing-service.
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{productId}/update-benchmark")]
    public async Task<ActionResult<MarketPriceResolution>> UpdateBenchmark(
        Guid productId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Iniciando atualização de benchmark para produto: {ProductId}", productId);

            var resolution = await _marketDataService.UpdateBenchmarkForExistingProductAsync(
                productId, 
                $"Produto_{productId}", // Em produção, usar o nome real do produto
                ct);

            if (resolution == null)
            {
                _logger.LogWarning("Atualização de benchmark falhou para produto: {ProductId}", productId);
                return BadRequest("Falha ao atualizar benchmark - produto não encontrado ou pesquisa falhou");
            }

            _logger.LogInformation(
                "Benchmark atualizado com sucesso para produto {ProductId}: {ProductName} - Preço: {Price:C}",
                productId, 
                resolution.NormalizedProductName, 
                resolution.Quote.Price);

            return Ok(resolution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar benchmark para produto {ProductId}", productId);
            return StatusCode(500, "Erro ao atualizar benchmark");
        }
    }

    /// <summary>
    /// Atualiza benchmarks para múltiplos produtos (batch).
    /// Usado por cron jobs de manutenção.
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("batch-update-benchmark")]
    public async Task<ActionResult<IEnumerable<MarketPriceResolution>>> BatchUpdateBenchmark(
        [FromBody] IEnumerable<Guid> productIds,
        CancellationToken ct)
    {
        if (!productIds.Any())
        {
            _logger.LogWarning("Lista de produtos vazia para batch update");
            return Ok(Array.Empty<MarketPriceResolution>());
        }

        _logger.LogInformation(
            "Iniciando batch update de benchmark para {Count} produtos", 
            productIds.Count());

        var results = await Task.WhenAll(productIds.Select(id => 
            UpdateBenchmark(id, ct)));

        var successfulResults = results.Where(r => r != null).ToList();
        
        _logger.LogInformation(
            "Batch update concluído: {Success}/{Total} produtos atualizados com sucesso",
            successfulResults.Count,
            productIds.Count());

        return Ok(successfulResults);
    }
}
