using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Benchmarking;
using Simcag.MarketDataService.Application.Interfaces;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Domain.ValueObjects;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Garante observações curadas no PostgreSQL para agregados GET /benchmarks (MVP seed).
/// </summary>
public sealed class CuratedBenchmarkSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CuratedBenchmarkSeedHostedService> _log;

    public CuratedBenchmarkSeedHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CuratedBenchmarkSeedHostedService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var env = scope.ServiceProvider.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        if (string.Equals(env?.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase))
            return;

        var repo = scope.ServiceProvider.GetRequiredService<IMarketPriceRepository>();

        foreach (var (seedName, category, price) in CuratedCategoryBenchmarkCatalog.SeedObservations)
        {
            var existing = await repo.GetByProductNameAsync(seedName, cancellationToken);
            if (existing is not null)
                continue;

            var obs = MarketPrice.CreateObservation(
                seedName,
                price,
                CuratedCategoryBenchmarkCatalog.SourcePrefix,
                category,
                GeographicRegion.DefaultSeedRegion);
            await repo.AddAsync(obs, cancellationToken);
            _log.LogInformation("Seed curado: {Name} {Category} R$ {Price}", seedName, category, price);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
