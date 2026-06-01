using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Simcag.MarketDataService.Domain.Entities;
using Simcag.MarketDataService.Infrastructure.Persistence.DbContext;

namespace Simcag.MarketDataService.Tests.Integration;

public sealed class MarketDataApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, MarketDataTestSeedStartupFilter>();
        });
    }
}

internal sealed class MarketDataTestSeedStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
            if (!db.MarketPrices.Any())
            {
                db.MarketPrices.AddRange(
                    MarketPrice.CreateObservation(
                        "Água sanitária 5L",
                        12.5m,
                        "integration-seed",
                        "Limpeza Predial",
                        "BR-SP"),
                    MarketPrice.CreateObservation(
                        "Desinfetante",
                        15.0m,
                        "integration-seed",
                        "Limpeza Predial",
                        "BR-SP"));
                db.SaveChanges();
            }

            const string historyProduct = "Detergente 5L";
            if (!db.MarketPriceHistory.Any(h => h.ProductName == historyProduct))
            {
                db.MarketPriceHistory.Add(
                    MarketPriceHistory.Create(
                        historyProduct,
                        40m,
                        "integration-seed",
                        DateTime.UtcNow.AddDays(-2)));
                db.SaveChanges();
            }

            next(app);
        };
    }
}
