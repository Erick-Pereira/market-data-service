namespace Simcag.MarketDataService.Application.Ports;

public sealed record MarketDataUpdatedEvent(string Category, string Region, DateTime OccurredAtUtc);

public interface IMarketDataUpdatedEventPublisher
{
    Task PublishAsync(MarketDataUpdatedEvent evt, CancellationToken ct);
}
