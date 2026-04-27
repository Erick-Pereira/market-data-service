using Microsoft.AspNetCore.Mvc;
using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Application.Queries;
using Simcag.Shared.Contracts;

namespace Simcag.MarketDataService.Api.Controllers;

[ApiController]
[Route("market-data")]
public sealed class MarketDataBenchmarkController : ControllerBase
{
    private readonly IMarketBenchmarkQuery _benchmarkQuery;

    public MarketDataBenchmarkController(IMarketBenchmarkQuery benchmarkQuery) =>
        _benchmarkQuery = benchmarkQuery;

    /// <summary>Aggregated market reference for benchmarking (category × region).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] string category, [FromQuery] string region, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(region))
        {
            return BadRequest(ApiResponse<string>.Fail(
                "category and region query parameters are required (MissingQueryParameters)"));
        }

        try
        {
            var dto = await _benchmarkQuery.GetAsync(category, region, ct);
            return Ok(ApiResponse<MarketDataResponseDto>.Ok(dto));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }
}
