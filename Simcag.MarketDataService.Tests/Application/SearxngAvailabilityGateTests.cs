using Simcag.MarketDataService.Application.Benchmarking;

namespace Simcag.MarketDataService.Tests.Application;

public sealed class SearxngAvailabilityGateTests
{
    [Fact]
    public void MarkUnavailable_bloqueia_ate_cooldown_expirar()
    {
        var gate = new SearxngAvailabilityGate();
        Assert.True(gate.IsAvailable);

        gate.MarkUnavailable(TimeSpan.FromMilliseconds(50));
        Assert.False(gate.IsAvailable);

        Thread.Sleep(60);
        Assert.True(gate.IsAvailable);
    }

    [Fact]
    public void MarkAvailable_reabre_imediatamente()
    {
        var gate = new SearxngAvailabilityGate();
        gate.MarkUnavailable(TimeSpan.FromMinutes(5));
        gate.MarkAvailable();
        Assert.True(gate.IsAvailable);
    }
}
