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
            var client = _httpFactory.CreateClient(MarketDataHttpClients.Searxng);
            using var rootCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Max(3, _opt.SearxngConnectTimeoutSeconds + 2)));
            using var resp = await client.GetAsync(baseUrl + "/", rootCts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                MarkUnavailableAndWarn(baseUrl, $"HTTP {(int)resp.StatusCode}");
                return;
            }

            _gate.MarkAvailable();

            var searchUrl = baseUrl + "/search?q=ping&format=json&language=pt-BR";
            using var searchCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Max(10, _opt.SearxngSearchTimeoutSeconds)));
            try
            {
                using var searchResp = await client.GetAsync(searchUrl, searchCts.Token).ConfigureAwait(false);
                if (searchResp.IsSuccessStatusCode)
                {
                    _log.LogInformation("SearXNG acessível em {BaseUrl} (JSON /search OK)", baseUrl);
                    return;
                }

                if (searchResp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _gate.MarkUnavailable(TimeSpan.FromMinutes(Math.Max(1, _opt.SearxngUnavailableCooldownMinutes)));
                }

                _log.LogWarning(
                    "SearXNG responde em {BaseUrl}/ mas /search retorna HTTP {(Status)}. " +
                    "No host: SEARXNG_LIMITER=false ou limiter.toml com pass_ip/trusted_proxies; " +
                    "MARKET_DATA__SEARXNG_CLIENT_IP numa faixa pass_ip (ex. 10.0.0.1).",
                    baseUrl,
                    (int)searchResp.StatusCode);
            }
            catch (OperationCanceledException)
            {
                // /search lento no 1º pedido não deve abrir circuit breaker — SearXNG está de pé.
                _log.LogWarning(
                    "SearXNG /search timeout no probe ({Timeout}s) em {BaseUrl}; provider mantido activo.",
                    _opt.SearxngSearchTimeoutSeconds,
                    baseUrl);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            MarkUnavailableAndWarn(baseUrl, "timeout de ligação (container provavelmente parado)");
        }
        catch (Exception ex)
        {
            MarkUnavailableAndWarn(baseUrl, ex.GetType().Name);
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
