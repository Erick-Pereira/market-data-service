namespace Simcag.MarketDataService.Application.Ports;

public sealed class NoOpMarketDataUpdatedEventPublisher : IMarketDataUpdatedEventPublisher
{
    public Task PublishAsync(MarketDataUpdatedEvent evt, CancellationToken ct) => Task.CompletedTask;
}
