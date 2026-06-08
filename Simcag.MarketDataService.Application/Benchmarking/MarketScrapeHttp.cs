using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Simcag.MarketDataService.Application.Configuration;
using Simcag.Shared.Telemetry;

namespace Simcag.MarketDataService.Application.Benchmarking;

internal static class MarketScrapeHttp
{
    /// <summary>
    /// GET com retentativas: 202, 429, 5xx. Não retenta 4xx definitivos (exceto 429).
    /// </summary>
    public static async Task<(HttpStatusCode? Status, string? Body, string Outcome)> GetHtmlAsync(
        HttpClient client,
        string url,
        MarketResearchOptions opt,
        string providerId,
        ILogger log,
        CancellationToken ct,
        int? maxRetriesOverride = null)
    {
        var max = maxRetriesOverride ?? Math.Max(0, opt.ScrapeMaxRetries);
        Exception? lastEx = null;
        for (var attempt = 0; attempt <= max; attempt++)
        {
            try
            {
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                var code = resp.StatusCode;

                if (code == HttpStatusCode.Accepted)
                {
                    SimcagMeters.MarketDataScrapeHttpAccepted.Add(1,
                        new KeyValuePair<string, object?>("provider", providerId));
                    SimcagMeters.MarketDataBenchmarkRejections.Add(1,
                        new KeyValuePair<string, object?>("reason", "http_202"),
                        new KeyValuePair<string, object?>("provider", providerId));
                    if (attempt < max)
                    {
                        SimcagMeters.MarketDataScrapeRetries.Add(1,
                            new KeyValuePair<string, object?>("provider", providerId),
                            new KeyValuePair<string, object?>("cause", "202"));
                        await DelayBackoffAsync(opt, attempt, ct);
                        continue;
                    }

                    return (code, null, "deferred_202");
                }

                if (code == HttpStatusCode.TooManyRequests || (int)code >= 500)
                {
                    RecordHttpMetric(providerId, (int)code);
                    if (attempt < max)
                    {
                        SimcagMeters.MarketDataScrapeRetries.Add(1,
                            new KeyValuePair<string, object?>("provider", providerId),
                            new KeyValuePair<string, object?>("cause", "transient_http"));
                        await DelayBackoffAsync(opt, attempt, ct);
                        continue;
                    }

                    var failBody = await SafeReadBodyAsync(resp, ct);
                    return (code, failBody, "http_transient_exhausted");
                }

                if (!resp.IsSuccessStatusCode)
                {
                    RecordHttpMetric(providerId, (int)code);
                    var body = await SafeReadBodyAsync(resp, ct);
                    return (code, body, "http_not_success");
                }

                var html = await resp.Content.ReadAsStringAsync(ct);
                return (code, html, "ok");
            }
            catch (Exception ex) when (IsConnectionRefused(ex))
            {
                log.LogDebug(ex, "Conexão recusada provider={Provider}", providerId);
                return (null, null, "connection_refused");
            }
            catch (Exception ex) when (ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                lastEx = ex;
                if (attempt < max)
                {
                    SimcagMeters.MarketDataScrapeRetries.Add(1,
                        new KeyValuePair<string, object?>("provider", providerId),
                        new KeyValuePair<string, object?>("cause", "timeout"));
                    await DelayBackoffAsync(opt, attempt, ct);
                    continue;
                }

                log.LogWarning(ex, "Scrape timeout esgotado provider={Provider}", providerId);
                return (null, null, "exception");
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < max)
                {
                    SimcagMeters.MarketDataScrapeRetries.Add(1,
                        new KeyValuePair<string, object?>("provider", providerId),
                        new KeyValuePair<string, object?>("cause", "exception"));
                    await DelayBackoffAsync(opt, attempt, ct);
                    continue;
                }

                log.LogWarning(ex, "Scrape falhou provider={Provider}", providerId);
                return (null, null, "exception");
            }
        }

        if (lastEx is not null)
            log.LogWarning(lastEx, "Scrape loop inesperado provider={Provider}", providerId);
        return (null, null, "exception");
    }

    private static async Task DelayBackoffAsync(MarketResearchOptions opt, int attempt, CancellationToken ct)
    {
        var ms = Math.Min(30_000, opt.ScrapeRetryBaseDelayMilliseconds * (1 << attempt));
        await Task.Delay(ms, ct);
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    private static void RecordHttpMetric(string providerId, int statusCode)
    {
        SimcagMeters.MarketDataScrapeHttpErrors.Add(1,
            new KeyValuePair<string, object?>("source", providerId),
            new KeyValuePair<string, object?>("status_code", statusCode));
    }

    private static bool IsConnectionRefused(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
                return true;
        }

        return false;
    }
}
