namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Evita bloquear o pipeline quando SearXNG não está a correr (connection refused).
/// Após a primeira falha, ignora pedidos durante o cooldown e tenta de novo depois.
/// </summary>
public sealed class SearxngAvailabilityGate
{
    private long _unavailableUntilUtcTicks;
    private int _startupWarningLogged;

    public bool IsAvailable =>
        DateTime.UtcNow.Ticks >= Volatile.Read(ref _unavailableUntilUtcTicks);

    public void MarkUnavailable(TimeSpan cooldown) =>
        Volatile.Write(ref _unavailableUntilUtcTicks, DateTime.UtcNow.Add(cooldown).Ticks);

    public void MarkAvailable() =>
        Volatile.Write(ref _unavailableUntilUtcTicks, 0);

    /// <summary>Devolve true na primeira vez que entra em cooldown (para log único).</summary>
    public bool TryLogStartupWarningOnce()
    {
        if (Interlocked.Exchange(ref _startupWarningLogged, 1) != 0)
            return false;
        return true;
    }
}
