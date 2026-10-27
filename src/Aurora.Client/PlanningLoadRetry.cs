using System.Net;

namespace Aurora.Client;

// Only for reads and the repeatable planning snapshot refresh, never plan/export writes.
internal static class PlanningLoadRetry
{
    internal static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken = default,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await send();
                if (attempt == 2 || response.StatusCode is not (HttpStatusCode.RequestTimeout
                    or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
                    return response;
                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < 2) { }
            catch (OperationCanceledException) when (attempt < 2 && !cancellationToken.IsCancellationRequested) { }
            await delay(TimeSpan.FromSeconds(attempt + 1), cancellationToken);
        }
    }
}
