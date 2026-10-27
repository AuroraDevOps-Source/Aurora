using System.Net;
using Aurora.Client;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
foreach (var status in new[] { 408, 502, 503, 504 })
{
    var calls = 0;
    var delays = new List<double>();
    using var result = await PlanningLoadRetry.SendAsync(() => Task.FromResult(new HttpResponseMessage(
        (HttpStatusCode)(++calls < 3 ? status : 200))), delay: (time, _) => { delays.Add(time.TotalSeconds); return Task.CompletedTask; });
    Check(result.IsSuccessStatusCode && calls == 3 && delays.SequenceEqual(new[] { 1d, 2d }), $"Recovery from {status}");
}
foreach (var status in new[] { 200, 400, 401, 403, 404, 409, 500 })
{
    var calls = 0;
    using var result = await PlanningLoadRetry.SendAsync(() => { calls++; return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)); },
        delay: (_, _) => throw new Exception("Unexpected retry"));
    Check(calls == 1 && (int)result.StatusCode == status, $"No retry for {status}");
}
var failures = 0;
using (var result = await PlanningLoadRetry.SendAsync(() => { failures++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }, delay: (_, _) => Task.CompletedTask))
    Check(failures == 3 && result.StatusCode == HttpStatusCode.ServiceUnavailable, "Bounded failures retain final error");
foreach (var timeout in new[] { false, true })
{
    var calls = 0;
    using var result = await PlanningLoadRetry.SendAsync(() => ++calls == 1
        ? Task.FromException<HttpResponseMessage>(timeout ? new TaskCanceledException() : new HttpRequestException())
        : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)), delay: (_, _) => Task.CompletedTask);
    Check(calls == 2 && result.IsSuccessStatusCode, "Transport recovery");
}
var attempts = 0;
try
{
    await PlanningLoadRetry.SendAsync(() => { attempts++; throw new HttpRequestException(); }, delay: (_, _) => Task.CompletedTask);
    throw new Exception("Expected transport failure");
}
catch (HttpRequestException) { Check(attempts == 3, "Bounded transport failures"); }
using var cts = new CancellationTokenSource();
var cancelledCalls = 0;
try
{
    await PlanningLoadRetry.SendAsync(() => { cancelledCalls++; cts.Cancel(); throw new TaskCanceledException(); }, cts.Token,
        delay: (_, _) => throw new Exception("Cancellation must not retry"));
    throw new Exception("Expected cancellation");
}
catch (OperationCanceledException) { Check(cancelledCalls == 1, "Caller cancellation is not retried"); }
Console.WriteLine($"{checks} planning retry checks passed.");
