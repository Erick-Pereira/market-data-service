using System.Text.RegularExpressions;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Referências curadas (sem API paga) para categorias condominiais e CFTV — complemento à pesquisa web.
/// Valores são medianas indicativas nacionais (BRL) para auditoria, não cotação comercial vinculante.
/// </summary>
public static class CuratedCategoryBenchmarkCatalog
{
    public const string SourcePrefix = "CuratedCategoryBenchmark";

    public sealed record MatchResult(
        decimal ReferencePriceBrl,
        string Category,
        string PatternId,
        string EvidenceSnippet);

    private sealed record Rule(
        string PatternId,
        string Category,
        decimal ReferencePriceBrl,
        string Evidence,
        Func<string, bool> Predicate);

    private static readonly Rule[] Rules =
    [
        new(
            "nvr_16_ip",
            "Segurança Eletrônica",
            2200m,
            "NVR 16 canais IP — faixa típica mercado BR (varejo online, 2024–2026).",
            u => u.Contains("NVR", StringComparison.Ordinal) && Regex.IsMatch(u, @"\b16\b")),
        new(
            "nvr_ip",
            "Segurança Eletrônica",
            1800m,
            "NVR IP — referência mediana mercado BR.",
            u => u.Contains("NVR", StringComparison.Ordinal)),
        new(
            "camera_ip_2mp",
            "Segurança Eletrônica",
            185m,
            "Câmera IP 2MP bullet/dome — mediana Mercado Livre / varejo BR.",
            u => (u.Contains("CAMERA", StringComparison.Ordinal) || u.Contains("CÂMERA", StringComparison.Ordinal)
                  || u.Contains("CFTV", StringComparison.Ordinal))
                 && (u.Contains("2MP", StringComparison.Ordinal) || u.Contains("IP", StringComparison.Ordinal))),
        new(
            "camera_ip",
            "Segurança Eletrônica",
            150m,
            "Câmera IP — referência unitária mercado BR.",
            u => u.Contains("CAMERA", StringComparison.Ordinal) || u.Contains("CÂMERA", StringComparison.Ordinal)),
        new(
            "elevador_manutencao",
            "Manutenção",
            1800m,
            "Manutenção preventiva/corretiva elevador — contrato mensal condomínio médio BR.",
            u => u.Contains("ELEVADOR", StringComparison.Ordinal)),
        new(
            "manutencao_predial",
            "Manutenção",
            2000m,
            "Manutenção predial / material de manutenção — pacote mensal típico condomínio BR.",
            u => u.Contains("MANUTEN", StringComparison.Ordinal)
                 && (u.Contains("PREDIAL", StringComparison.Ordinal) || u.Contains("MATERIAL", StringComparison.Ordinal))),
        new(
            "limpeza_condominio",
            "Serviços",
            3500m,
            "Serviço de limpeza condominial — contrato mensal referência nacional.",
            u => u.Contains("LIMPEZA", StringComparison.Ordinal)),
        new(
            "portaria_seguranca",
            "Serviços",
            8500m,
            "Portaria / vigilância condomínio — contrato mensal referência BR.",
            u => (u.Contains("PORTARIA", StringComparison.Ordinal) || u.Contains("VIGIL", StringComparison.Ordinal)
                  || u.Contains("SEGURAN", StringComparison.Ordinal))
                 && !u.Contains("CAMERA", StringComparison.Ordinal)
                 && !u.Contains("CFTV", StringComparison.Ordinal)
                 && !u.Contains("NVR", StringComparison.Ordinal)),
    ];

    public static MatchResult? TryMatch(string productName, decimal? declaredReferenceBrl = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return null;

        var upper = productName.Trim().ToUpperInvariant();
        foreach (var rule in Rules)
        {
            if (!rule.Predicate(upper))
                continue;

            if (declaredReferenceBrl is > 0.01m
                && !DeclaredReferencePlausibility.IsPlausible(rule.ReferencePriceBrl, declaredReferenceBrl.Value))
                continue;

            return new MatchResult(rule.ReferencePriceBrl, rule.Category, rule.PatternId, rule.Evidence);
        }

        return null;
    }

    public static IReadOnlyList<(string SeedProductName, string Category, decimal Price)> SeedObservations =>
    [
        ("__curated__camera_ip_2mp", "Segurança Eletrônica", 185m),
        ("__curated__nvr_16_ip", "Segurança Eletrônica", 2200m),
        ("__curated__manutencao_predial_mensal", "Manutenção", 2000m),
        ("__curated__elevador_manutencao_mensal", "Manutenção", 1800m),
        ("__curated__limpeza_condominio_mensal", "Serviços", 3500m),
        ("__curated__portaria_condominio_mensal", "Serviços", 8500m),
    ];
}
