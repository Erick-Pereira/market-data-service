namespace Simcag.MarketDataService.Domain.Entities;

public class MarketPrice
{
    public Guid Id { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public DateTime CollectedDate { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>Expense category for aggregated benchmarking.</summary>
    public string ExpenseCategory { get; private set; } = string.Empty;
    /// <summary>Geographic region key (e.g. BR-SP).</summary>
    public string GeographicRegion { get; private set; } = string.Empty;
    public string SupplierProfile { get; private set; } = string.Empty;

    private MarketPrice() { }

    public static MarketPrice Create(string productName, decimal price, string source)
    {
        return new MarketPrice
        {
            Id = Guid.NewGuid(),
            ProductName = productName,
            Price = price,
            Source = source,
            CollectedDate = DateTime.UtcNow,
            IsActive = true,
            ExpenseCategory = string.Empty,
            GeographicRegion = string.Empty,
            SupplierProfile = string.Empty
        };
    }

    /// <summary>Rehydrates a quote returned from cache without mutating collected timestamp semantics.</summary>
    public static MarketPrice FromCachedQuote(string productName, decimal price, string source, DateTime collectedDateUtc)
    {
        return new MarketPrice
        {
            Id = Guid.NewGuid(),
            ProductName = productName,
            Price = price,
            Source = source,
            CollectedDate = collectedDateUtc,
            IsActive = true,
            ExpenseCategory = string.Empty,
            GeographicRegion = string.Empty,
            SupplierProfile = string.Empty
        };
    }

    public static MarketPrice CreateObservation(
        string productName,
        decimal price,
        string source,
        string expenseCategory,
        string geographicRegion,
        string supplierProfile = "")
    {
        return new MarketPrice
        {
            Id = Guid.NewGuid(),
            ProductName = productName,
            Price = price,
            Source = source,
            CollectedDate = DateTime.UtcNow,
            IsActive = true,
            ExpenseCategory = expenseCategory.Trim(),
            GeographicRegion = geographicRegion.Trim(),
            SupplierProfile = (supplierProfile ?? string.Empty).Trim()
        };
    }

    public void UpdatePrice(decimal newPrice)
    {
        Price = newPrice;
        CollectedDate = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}