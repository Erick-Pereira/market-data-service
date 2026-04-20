using StackExchange.Redis;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Simcag.MarketDataService.Application.Services;

public class MarketDataCacheService : IMarketDataCacheService
{
    private readonly IDatabase _redisDb;
    private readonly ILogger<MarketDataCacheService> _logger;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(1);

    public MarketDataCacheService(
        IConnectionMultiplexer redisConnection,
        ILogger<MarketDataCacheService> logger)
    {
        _redisDb = redisConnection.GetDatabase();
        _logger = logger;
    }

    public async Task<MarketPrice?> GetMarketPriceAsync(string productName, CancellationToken ct)
    {
        try
        {
            var key = GetCacheKey(productName);
            var cachedValue = await _redisDb.StringGetAsync(key);

            if (!cachedValue.IsNullOrEmpty)
            {
                var marketPrice = JsonSerializer.Deserialize<MarketPrice>((string)cachedValue!);
                _logger.LogInformation("Cache hit for product {ProductName}: {Price:C}", productName, marketPrice?.Price);
                return marketPrice;
            }

            _logger.LogInformation("Cache miss for product {ProductName}", productName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache for product {ProductName}", productName);
            return null;
        }
    }

    public async Task SetMarketPriceAsync(MarketPrice marketPrice, CancellationToken ct)
    {
        try
        {
            var key = GetCacheKey(marketPrice.ProductName);
            var serializedValue = JsonSerializer.Serialize(marketPrice);

            var success = await _redisDb.StringSetAsync(key, serializedValue, _defaultTtl);

            if (success)
            {
                _logger.LogInformation("Cached market price for product {ProductName}: {Price:C} (TTL: {Ttl})",
                    marketPrice.ProductName, marketPrice.Price, _defaultTtl);
            }
            else
            {
                _logger.LogWarning("Failed to cache market price for product {ProductName}", marketPrice.ProductName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for product {ProductName}", marketPrice.ProductName);
        }
    }

    public async Task RemoveMarketPriceAsync(string productName, CancellationToken ct)
    {
        try
        {
            var key = GetCacheKey(productName);
            var success = await _redisDb.KeyDeleteAsync(key);

            if (success)
            {
                _logger.LogInformation("Removed cache entry for product {ProductName}", productName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache for product {ProductName}", productName);
        }
    }

    public async Task ClearAllCacheAsync(CancellationToken ct)
    {
        try
        {
            var endpoints = _redisDb.Multiplexer.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _redisDb.Multiplexer.GetServer(endpoint);
                await server.FlushDatabaseAsync();
            }

            _logger.LogInformation("Cleared all market data cache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    private string GetCacheKey(string productName)
    {
        // Normalize product name for consistent caching
        return $"marketprice:{productName.ToLower().Trim()}";
    }
}