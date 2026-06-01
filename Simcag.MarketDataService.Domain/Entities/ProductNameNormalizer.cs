namespace Simcag.MarketDataService.Domain.Entities;

/// <summary>
/// Normalizador de nomes de produtos — remove acentos, símbolos e converte para maiúsculo.
/// </summary>
public static class ProductNameNormalizer
{
    // Caracteres comuns que devem ser removidos (acentos e outros)
    private static readonly string[] Accents = { "À", "Á", "Â", "Ã", "Ä", "Å", "Æ", "Ç", "È", "É", "Ê", "Ë", 
        "Ì", "Í", "Î", "Ï", "Ð", "Ñ", "Ò", "Ó", "Ô", "Õ", "Ö", "×", "Ø", "Ù", "Ú", "Û", "Ü", "Ý", "Þ", "ß", 
        "à", "á", "â", "ã", "ä", "å", "æ", "ç", "è", "é", "ê", "ë", "ì", "í", "î", "ï", "ð", "ñ", "ò", "ó", 
        "ô", "õ", "ö", "÷", "ø", "ù", "ú", "û", "ü", "ý", "þ", "ÿ" };
    
    private static readonly string Replacement = " ";

    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Criar cópia mutável
        var result = new string(raw.ToCharArray());
        
        // Substituir acentos por espaços vazios
        foreach (var accent in Accents)
        {
            result = result.Replace(accent, Replacement);
        }

        // Converter para maiúsculo e limpar
        return result.Trim()
            .ToUpperInvariant()
            .Replace(" ", "-")
            .Replace("-", " ")  // Manter separador legível
            .Replace("  ", " "); // Remover duplo espaço
    }
}
