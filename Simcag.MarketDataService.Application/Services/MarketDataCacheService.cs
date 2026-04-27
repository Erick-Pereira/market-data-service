using System.Text.Json;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Cache;
using Simcag.MarketDataService.Application.DTOs;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using StackExchange.Redis;

namespace Simcag.MarketDataService.Application.Services;

public class MarketDataCacheService : IMarketDataCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
            var key = GetProductCacheKey(productName);
            var cachedValue = await _redisDb.StringGetAsync(key);

            if (cachedValue.IsNullOrEmpty)
            {
                _logger.LogInformation("Cache miss for product {ProductName}", productName);
                return null;
            }

            var entry = JsonSerializer.Deserialize<MarketPriceCacheEntry>((string)cachedValue!, JsonOptions);
            if (entry == null)
                return null;

            var marketPrice = MarketPrice.FromCachedQuote(entry.ProductName, entry.Price, entry.Source, entry.CollectedDate);
            _logger.LogInformation("Cache hit for product {ProductName}: {Price:C}", productName, entry.Price);
            return marketPrice;
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
            var key = GetProductCacheKey(marketPrice.ProductName);
            var entry = new MarketPriceCacheEntry
            {
                ProductName = marketPrice.ProductName,
                Price = marketPrice.Price,
                Source = marketPrice.Source,
                CollectedDate = marketPrice.CollectedDate
            };
            var serializedValue = JsonSerializer.Serialize(entry, JsonOptions);
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
            var key = GetProductCacheKey(productName);
            var success = await _redisDb.KeyDeleteAsync(key);

            if (success)
                _logger.LogInformation("Removed cache entry for product {ProductName}", productName);
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

    public async Task<MarketDataResponseDto?> GetBenchmarkAsync(string category, string region, CancellationToken ct)
    {
        try
        {
            var key = GetBenchmarkCacheKey(category, region);
            var cachedValue = await _redisDb.StringGetAsync(key);
            if (cachedValue.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<MarketDataResponseDto>((string)cachedValue!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading benchmark cache for {Category}/{Region}", category, region);
            return null;
        }
    }

    public async Task SetBenchmarkAsync(MarketDataResponseDto dto, CancellationToken ct)
    {
        try
        {
            var key = GetBenchmarkCacheKey(dto.Category, dto.Region);
            var json = JsonSerializer.Serialize(dto, JsonOptions);
            await _redisDb.StringSetAsync(key, json, _defaultTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing benchmark cache for {Category}/{Region}", dto.Category, dto.Region);
        }
    }

    private static string GetProductCacheKey(string productName) =>
        $"marketprice:{productName.ToLowerInvariant().Trim()}";

    private static string GetBenchmarkCacheKey(string category, string region) =>
        $"marketdata:bench:{category.ToLowerInvariant().Trim()}:{region.ToLowerInvariant().Trim()}";
}
