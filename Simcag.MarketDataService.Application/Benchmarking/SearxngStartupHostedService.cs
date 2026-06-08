using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simcag.MarketDataService.Application;
using Simcag.MarketDataService.Application.Configuration;

namespace Simcag.MarketDataService.Application.Benchmarking;

/// <summary>
/// Probe não bloqueante no arranque — avisa se SearXNG está configurado mas inacessível.
/// Falha de ligação nunca impede a API de subir (benchmark curado + DDG/Bing continuam).
/// </summary>
public sealed class SearxngStartupHostedService : IHostedService
{
    private readonly MarketResearchOptions _opt;
    private readonly SearxngAvailabilityGate _gate;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IHostEnvironment _env;
    private readonly ILogger<SearxngStartupHostedService> _log;

    public SearxngStartupHostedService(
        IOptions<MarketResearchOptions> options,
        SearxngAvailabilityGate gate,
        IHttpClientFactory httpFactory,
        IHostEnvironment env,
        ILogger<SearxngStartupHostedService> log)
    {
        _opt = options.Value;
        _gate = gate;
        _httpFactory = httpFactory;
        _env = env;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_env.IsEnvironment("Testing"))
            return Task.CompletedTask;

        if (!_opt.EnableSearxngScrape || string.IsNullOrWhiteSpace(_opt.SearxngBaseUrl))
            return Task.CompletedTask;

        _ = ProbeInBackgroundAsync();
        return Task.CompletedTask;
    }

    private async Task ProbeInBackgroundAsync()
    {
        var baseUrl = _opt.SearxngBaseUrl!.Trim().TrimEnd('/');
        try
        {
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Max(2, _opt.SearxngConnectTimeoutSeconds + 1)));
            var client = _httpFactory.CreateClient(MarketDataHttpClients.Searxng);
            using var resp = await client.GetAsync(baseUrl + "/", cts.Token).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                _gate.MarkAvailable();
                _log.LogInformation("SearXNG acessível em {BaseUrl}", baseUrl);
                return;
            }

            MarkUnavailableAndWarn(baseUrl, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            var reason = ex is OperationCanceledException or TimeoutException
                ? "timeout de ligação (container provavelmente parado)"
                : ex.GetType().Name;
            MarkUnavailableAndWarn(baseUrl, reason);
        }
    }

    private void MarkUnavailableAndWarn(string baseUrl, string reason)
    {
        _gate.MarkUnavailable(TimeSpan.FromMinutes(Math.Max(1, _opt.SearxngUnavailableCooldownMinutes)));
        if (!_gate.TryLogStartupWarningOnce())
            return;

        _log.LogWarning(
            "SearXNG indisponível em {BaseUrl} ({Reason}). API OK — benchmarks curados + DDG/Bing activos. Para activar: docker compose -f docker-compose.dev.yml up -d searxng",
            baseUrl,
            reason);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
