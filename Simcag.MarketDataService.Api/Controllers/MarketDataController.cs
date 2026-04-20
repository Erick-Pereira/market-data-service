using Microsoft.AspNetCore.Mvc;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.Shared.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;

    public MarketDataController(IMarketDataService marketDataService)
    {
        _marketDataService = marketDataService;
    }

    [HttpGet("price")]
    public async Task<IActionResult> GetPrice([FromQuery] string productName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return BadRequest(new ApiResponse<string>(false, "Product name is required", null, ["ProductNameRequired"]));
        }

        var marketPrice = await _marketDataService.GetPriceAsync(productName, ct);

        if (marketPrice == null)
        {
            return NotFound(new ApiResponse<string>(false, $"No market price found for product: {productName}", null, ["PriceNotFound"]));
        }

        var result = new
        {
            marketPrice.ProductName,
            marketPrice.Price,
            marketPrice.Source,
            marketPrice.CollectedDate
        };

        return Ok(new ApiResponse<object>(true, "Market price retrieved successfully", result, null));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetPriceHistory(
        [FromQuery] string productName,
        [FromQuery] int days = 30,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return BadRequest(new ApiResponse<string>(false, "Product name is required", null, ["ProductNameRequired"]));
        }

        if (days <= 0 || days > 365)
        {
            return BadRequest(new ApiResponse<string>(false, "Days must be between 1 and 365", null, ["InvalidDaysRange"]));
        }

        var history = await _marketDataService.GetPriceHistoryAsync(productName, days, ct);

        var result = history.Select(h => new
        {
            h.ProductName,
            h.Price,
            h.Source,
            h.CollectedDate
        });

        return Ok(new ApiResponse<object>(true, $"Retrieved {result.Count()} historical prices", result, null));
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedMockData(CancellationToken ct)
    {
        await _marketDataService.SeedMockDataAsync(ct);

        return Ok(new ApiResponse<string>(true, "Mock market data seeded successfully", null, null));
    }
}