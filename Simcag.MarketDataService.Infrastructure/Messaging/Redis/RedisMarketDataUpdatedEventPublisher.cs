using System.Text.Json;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Ports;
using Simcag.MarketDataService.Domain.Constants;
using StackExchange.Redis;

namespace Simcag.MarketDataService.Infrastructure.Messaging.Redis;

public sealed class RedisMarketDataUpdatedEventPublisher : IMarketDataUpdatedEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisMarketDataUpdatedEventPublisher> _logger;

    public RedisMarketDataUpdatedEventPublisher(
        IConnectionMultiplexer multiplexer,
        ILogger<RedisMarketDataUpdatedEventPublisher> logger)
    {
        _subscriber = multiplexer.GetSubscriber();
        _logger = logger;
    }

    public async Task PublishAsync(MarketDataUpdatedEvent evt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { evt.Category, evt.Region, evt.OccurredAtUtc }, JsonOptions);
        var channel = RedisChannel.Literal(EventNames.MarketDataUpdatedEvents);
        var receivers = await _subscriber.PublishAsync(channel, payload).ConfigureAwait(false);
        _logger.LogDebug("Published {Event} to Redis channel {Channel}; receivers={Receivers}", nameof(MarketDataUpdatedEvent), EventNames.MarketDataUpdatedEvents, receivers);
    }
}
