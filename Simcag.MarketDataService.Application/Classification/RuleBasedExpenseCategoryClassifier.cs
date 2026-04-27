namespace Simcag.MarketDataService.Application.Classification;

public sealed class RuleBasedExpenseCategoryClassifier : IRuleBasedExpenseCategoryClassifier
{
    public string Classify(string productNameOrDescription)
    {
        var name = productNameOrDescription.ToLowerInvariant();

        if (name.Contains("notebook") || name.Contains("laptop"))
            return "Notebook";
        if (name.Contains("monitor") || name.Contains("display"))
            return "Monitor";
        if (name.Contains("mouse") || name.Contains("keyboard") || name.Contains("teclado"))
            return "Periférico";
        if (name.Contains("ram") || name.Contains("ssd") || name.Contains("cpu") || name.Contains("processador") || name.Contains("placa"))
            return "Hardware";

        return "Outro";
    }
}
