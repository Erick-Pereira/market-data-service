namespace Simcag.MarketDataService.Application;

/// <summary>Nomes dos <see cref="System.Net.Http.HttpClient"/> registados em DI (market-data).</summary>
public static class MarketDataHttpClients
{
    public const string WebScrape = "MarketDataWebScrape";
    public const string Serp = "MarketDataSerpApi";
}
