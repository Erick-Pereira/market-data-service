using Microsoft.AspNetCore.Mvc;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.Shared.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;

    public MarketDataController(IMarketDataService marketDataService) => _marketDataService = marketDataService;

    [HttpGet("price")]
    public async Task<IActionResult> GetPrice([FromQuery] string productName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return BadRequest(ApiResponse<string>.Fail("Product name is required (ProductNameRequired)"));

        var marketPrice = await _marketDataService.GetPriceAsync(productName, ct);

        if (marketPrice == null)
            return NotFound(ApiResponse<string>.Fail($"No market price found for product: {productName} (PriceNotFound)"));

        var result = new
        {
            marketPrice.ProductName,
            marketPrice.Price,
            marketPrice.Source,
            marketPrice.CollectedDate
        };

        return Ok(ApiResponse<object>.Ok(result!));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetPriceHistory(
        [FromQuery] string productName,
        CancellationToken ct,
        [FromQuery] int days = 30)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return BadRequest(ApiResponse<string>.Fail("Product name is required (ProductNameRequired)"));

        if (days <= 0 || days > 365)
            return BadRequest(ApiResponse<string>.Fail("Days must be between 1 and 365 (InvalidDaysRange)"));

        var history = await _marketDataService.GetPriceHistoryAsync(productName, days, ct);

        var result = history.Select(h => new
        {
            h.ProductName,
            h.Price,
            h.Source,
            h.CollectedDate
        });

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("seed")]
    public async Task<IActionResult> SeedMockData(CancellationToken ct)
    {
        await _marketDataService.SeedMockDataAsync(ct);
        return Ok(ApiResponse<string>.Ok("Mock market data seeded successfully"));
    }
}
