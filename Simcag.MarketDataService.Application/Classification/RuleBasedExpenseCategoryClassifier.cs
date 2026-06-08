using System.Text.RegularExpressions;

namespace Simcag.MarketDataService.Application.Classification;

public sealed class RuleBasedExpenseCategoryClassifier : IRuleBasedExpenseCategoryClassifier
{
    public string Classify(string productNameOrDescription)
    {
        var name = productNameOrDescription.ToLowerInvariant();

        // Despesas condominiais / operacionais (PT-BR) — prioridade sobre palavras inglesas acidentais.
        if (name.Contains("elevador", StringComparison.Ordinal) || name.Contains("manuten", StringComparison.Ordinal))
            return "Manutenção";
        if (name.Contains("limpeza", StringComparison.Ordinal) || name.Contains("segur", StringComparison.Ordinal))
            return "Serviços";
        if (name.Contains("energia", StringComparison.Ordinal) || name.Contains("água", StringComparison.Ordinal)
            || name.Contains("agua", StringComparison.Ordinal))
            return "Utilidades";
        if (name.Contains("síndic", StringComparison.Ordinal) || name.Contains("sindic", StringComparison.Ordinal)
            || name.Contains("honor", StringComparison.Ordinal) || name.Contains("gest", StringComparison.Ordinal))
            return "Administrativo";
        if (name.Contains("fundo de reserva", StringComparison.Ordinal) || name.Contains("condom", StringComparison.Ordinal))
            return "Condomínio";

        if (Regex.IsMatch(name, @"\b(camera|câmera|nvr|cftv)\b", RegexOptions.IgnoreCase))
            return "Segurança Eletrônica";

        // Eletrónica — só com limite de palavra para não apanhar substrings em "manutenção", etc.
        if (Regex.IsMatch(name, @"\b(notebook|laptop)\b", RegexOptions.IgnoreCase))
            return "Notebook";
        if (Regex.IsMatch(name, @"\b(monitor|display)\b", RegexOptions.IgnoreCase))
            return "Monitor";
        if (Regex.IsMatch(name, @"\b(mouse|keyboard|teclado)\b", RegexOptions.IgnoreCase))
            return "Periférico";
        if (Regex.IsMatch(name, @"\b(ram|ssd|cpu|processador|placa)\b", RegexOptions.IgnoreCase))
            return "Hardware";

        return "Outro";
    }
}
