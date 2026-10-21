using System.Net.Http.Json;
using Aurora.Contracts;
using Microsoft.AspNetCore.Components;

namespace Aurora.Client;

public sealed class ClientErrorReporter(IHttpClientFactory clients, NavigationManager navigation)
{
    private DateTime _lastReport;
    public async Task Report(string kind)
    {
        if (DateTime.UtcNow - _lastReport < TimeSpan.FromSeconds(5)) return;
        _lastReport = DateTime.UtcNow;
        try
        {
            var path = new Uri(navigation.Uri).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var page = path.Length > 1 && path[0] == "aurora" ? path[1] : "Application";
            using var client = clients.CreateClient(ApiClients.Authorized);
            using var response = await client.PostAsJsonAsync("api/v1/applog/client-error", new ClientLogError(kind, page));
        }
        catch { /* Offline clients cannot send telemetry; preserve the original UI error. */ }
    }
}

public sealed class ClientErrorHandler(ClientErrorReporter reporter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try { return await base.SendAsync(request, ct); }
        catch (HttpRequestException)
        {
            if (request.RequestUri?.AbsolutePath != "/api/v1/applog/client-error") await reporter.Report("Network failure");
            throw;
        }
    }
}
