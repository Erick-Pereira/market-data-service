using Microsoft.AspNetCore.Mvc;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/market-data")]
public class MarketDataController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;

    public MarketDataController(IMarketDataService marketDataService) => _marketDataService = marketDataService;

    [HttpGet("price")]
    public async Task<IActionResult> GetPrice(
        [FromQuery] string? productName,
        [FromQuery] string? category,
        [FromQuery] string? region,
        [FromQuery] decimal? declaredReferenceBrl,
        CancellationToken ct)
    {
        var name = !string.IsNullOrWhiteSpace(productName)
            ? productName.Trim()
            : !string.IsNullOrWhiteSpace(category)
                ? string.Join(' ', new[] { category.Trim(), region?.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : null;

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<string>.Fail("Provide productName or category (ProductNameRequired)"));

        var marketPrice = await _marketDataService.GetPriceAsync(name, ct, declaredReferenceBrl);

        if (marketPrice == null)
            return NotFound(ApiResponse<string>.Fail($"No market price found for product: {name} (PriceNotFound)"));

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
        [FromQuery] string? productName,
        [FromQuery] string? category,
        [FromQuery] string? region,
        CancellationToken ct,
        [FromQuery] int days = 30)
    {
        var name = !string.IsNullOrWhiteSpace(productName)
            ? productName.Trim()
            : !string.IsNullOrWhiteSpace(category)
                ? string.Join(' ', new[] { category.Trim(), region?.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : null;

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(ApiResponse<string>.Fail("Provide productName or category (ProductNameRequired)"));

        if (days <= 0 || days > 365)
            return BadRequest(ApiResponse<string>.Fail("Days must be between 1 and 365 (InvalidDaysRange)"));

        var history = await _marketDataService.GetPriceHistoryAsync(name, days, ct);

        var result = history.Select(h => new
        {
            h.ProductName,
            h.Price,
            h.Source,
            h.CollectedDate
        });

        return Ok(ApiResponse<object>.Ok(result));
    }
}
