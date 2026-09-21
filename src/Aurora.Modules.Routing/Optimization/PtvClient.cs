using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Aurora.Modules.Routing.Optimization;

/// <summary>One HTTP call to PTV: the status line plus the body exactly as returned.</summary>
public sealed record PtvResponse(int StatusCode, string ReasonPhrase, string Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
    public string StatusLine => $"{StatusCode} {ReasonPhrase}";
}

/// <summary>
/// Outcome of a full submit + poll round trip. <see cref="Response"/> is whatever
/// PTV returned last — the terminal result on success, or the error body if the
/// submit failed / polling stopped early. <see cref="TimedOut"/> means polling hit
/// the configured timeout before a terminal status.
/// </summary>
public sealed record PtvRun(
    string? OptimizationId,
    string? Status,
    PtvResponse Response,
    TimeSpan Elapsed,
    bool TimedOut = false);

/// <summary>
/// Transport-only client for the PTV Route Optimization OptiFlow API, matching the
/// Integration Hub's PtvOptiFlowClient:
///   POST /optimizations      -> { "id": "..." }
///   GET  /optimizations/{id} -> result carrying "status" (QUEUING | PREPARING |
///                               RUNNING | STOPPING | SUCCEEDED | FAILED)
/// Auth is the "ApiKey" request header.
///
/// Bodies are passed through as raw strings — nothing is reshaped — so the UI can
/// show byte-for-byte what went out and what came back.
/// </summary>
public sealed class PtvClient : IDisposable
{
    private readonly PtvSettings _settings;
    private readonly HttpClient _http;

    public PtvClient(PtvSettings settings)
    {
        _settings = settings;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.RequestTimeoutSeconds))
        };
    }

    public string BaseUrl => _settings.BaseUrl.TrimEnd('/');

    public static bool IsTerminal(string? status) =>
        string.Equals(status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Submits the request JSON verbatim, then polls until the optimization reaches a
    /// terminal status, PTV returns an error, or the poll timeout elapses.
    /// </summary>
    public async Task<PtvRun> RunAsync(string requestJson, Action<string> log, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var bytes = Encoding.UTF8.GetByteCount(requestJson);

        log($"POST {BaseUrl}/optimizations  ({bytes:N0} bytes)");
        var submit = await PostAsync("/optimizations", requestJson, cancellationToken).ConfigureAwait(false);
        log($"  <- {submit.StatusLine}  ({stopwatch.ElapsedMilliseconds:N0} ms)");

        if (!submit.IsSuccess)
        {
            log("  submit failed — see the Response tab for PTV's error body.");
            return new PtvRun(null, null, submit, stopwatch.Elapsed);
        }

        if (!TryReadString(submit.Body, "id", out var id))
        {
            log("  submit succeeded but the body carried no \"id\" — nothing to poll.");
            return new PtvRun(null, null, submit, stopwatch.Elapsed);
        }

        log($"  optimization id = {id}");

        var interval = TimeSpan.FromSeconds(Math.Max(1, _settings.PollIntervalSeconds));
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(interval.TotalSeconds, _settings.PollTimeoutSeconds));
        var attempt = 0;

        while (true)
        {
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            attempt++;

            var poll = await GetAsync($"/optimizations/{Uri.EscapeDataString(id)}", cancellationToken).ConfigureAwait(false);
            var status = TryReadString(poll.Body, "status", out var s) ? s : null;
            log($"  poll #{attempt}: {poll.StatusLine}  status={status ?? "(none)"}  ({stopwatch.Elapsed.TotalSeconds:F1}s)");

            if (!poll.IsSuccess)
            {
                log("  poll failed — see the Response tab for PTV's error body.");
                return new PtvRun(id, status, poll, stopwatch.Elapsed);
            }

            if (IsTerminal(status))
            {
                log($"  done: {status} after {stopwatch.Elapsed.TotalSeconds:F1}s");
                return new PtvRun(id, status, poll, stopwatch.Elapsed);
            }

            if (DateTime.UtcNow >= deadline)
            {
                log($"  timed out after {_settings.PollTimeoutSeconds}s — last status {status ?? "(none)"}. " +
                    $"Showing the last body received; the job may still be running at PTV (id {id}).");
                return new PtvRun(id, status, poll, stopwatch.Elapsed, TimedOut: true);
            }
        }
    }

    private async Task<PtvResponse> PostAsync(string path, string json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PtvResponse> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PtvResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("ApiKey", _settings.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new PtvResponse((int)response.StatusCode, response.ReasonPhrase ?? response.StatusCode.ToString(), body);
    }

    /// <summary>Pulls a top-level string property out of a JSON body without binding to a DTO.</summary>
    private static bool TryReadString(string json, string property, out string value)
    {
        value = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(property, out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? "";
                return !string.IsNullOrEmpty(value);
            }
        }
        catch (JsonException)
        {
            // Non-JSON body (e.g. an HTML error page) — caller shows it verbatim.
        }
        return false;
    }

    public void Dispose() => _http.Dispose();
}
