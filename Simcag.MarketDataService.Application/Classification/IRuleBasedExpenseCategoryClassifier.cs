namespace Simcag.MarketDataService.Application.Classification;

/// <summary>Deterministic, rule-based mapping from product/description text to expense category (no ML).</summary>
public interface IRuleBasedExpenseCategoryClassifier
{
    string Classify(string productNameOrDescription);
}
