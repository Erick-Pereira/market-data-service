namespace Simcag.MarketDataService.Domain.ValueObjects;

/// <summary>Normalized expense category for market benchmarking (condominial context).</summary>
public readonly record struct ExpenseCategory(string Value)
{
    public string Normalized => string.IsNullOrWhiteSpace(Value)
        ? string.Empty
        : Value.Trim();

    public static ExpenseCategory FromInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new ExpenseCategory(string.Empty);
        var v = input.Trim();
        if (v.Length > 120)
            v = v[..120];
        return new ExpenseCategory(v);
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Normalized);
}
