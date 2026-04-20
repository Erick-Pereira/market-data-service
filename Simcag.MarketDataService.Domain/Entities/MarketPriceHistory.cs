namespace Simcag.MarketDataService.Domain.Entities;

public class MarketPriceHistory
{
    public Guid Id { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public DateTime CollectedDate { get; private set; }

    private MarketPriceHistory() { }

    public static MarketPriceHistory Create(string productName, decimal price, string source, DateTime collectedDate)
    {
        return new MarketPriceHistory
        {
            Id = Guid.NewGuid(),
            ProductName = productName,
            Price = price,
            Source = source,
            CollectedDate = collectedDate
        };
    }
}