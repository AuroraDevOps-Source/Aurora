using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Modules.Routing.Optimization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Aurora.Api.Features.Routing;

// Encrypted files share the persisted data-protection volume in the single-host installation.
// Multiple API replicas must share this storage AND use a distributed lock before enabling sessions.
public sealed class PlanningSessions
{
    private readonly string _directory;
    private readonly IDataProtector _protector;
    private readonly PtvSettings _settings;
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1)).ToArray();
    public PlanningSessions(IConfiguration config, IWebHostEnvironment environment, IDataProtectionProvider protection, IOptions<PtvSettings> settings)
    {
        _directory = config["Routing:SessionPath"] ?? Path.Combine(config["Hosting:DataProtectionPath"] ?? Path.Combine(environment.ContentRootPath, "App_Data"), "routing-sessions");
        _protector = protection.CreateProtector("Aurora.Routing.Sessions.v1");
        _settings = settings.Value;
        Directory.CreateDirectory(_directory);
        // Files are only session records in this dedicated directory; expire retained data after 48 hours.
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-2)) File.Delete(file);
    }
    public sealed record Saved(Guid Id, string Owner, string Fingerprint, string FileName, string RequestJson, string PreparedJson,
        DateTimeOffset Started, string? ProviderId = null, string Status = "SUBMITTING", OptimizationResultDto? Result = null, Guid? WorkspaceDraftId = null);

    public static string Owner(ClaimsPrincipal user)
    {
        var tenant = user.FindFirstValue("tenant_id");
        var subject = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(subject)) throw new UnauthorizedAccessException();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tenant + ":" + subject)));
    }
    private string PathFor(string owner, Guid id) => Path.Combine(_directory, owner + "-" + id.ToString("N") + ".json");
    private Saved? Read(string owner, Guid id)
    {
        var path = PathFor(owner, id);
        if (!File.Exists(path)) return null;
        var saved = JsonSerializer.Deserialize<Saved>(_protector.Unprotect(File.ReadAllText(path)));
        return saved?.Owner == owner && saved.Started > DateTimeOffset.UtcNow.AddDays(-2) ? saved : null;
    }
    private void Save(Saved saved)
    {
        var path = PathFor(saved.Owner, saved.Id);
        var temp = path + ".tmp";
        File.WriteAllText(temp, _protector.Protect(JsonSerializer.Serialize(saved)));
        File.Move(temp, path, true);
    }
    private SemaphoreSlim Gate(Guid id) => _locks[(id.GetHashCode() & int.MaxValue) % _locks.Length];
    public async Task<IResult> Start(StartPlanningDto input, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return Error(503, "PTV is not configured.");
        if (input.Id == Guid.Empty || string.IsNullOrWhiteSpace(input.RequestJson) ||
            Encoding.UTF8.GetByteCount(input.RequestJson) > 25 * 1024 * 1024 ||
            Encoding.UTF8.GetByteCount(input.PreviousResult ?? "") > 25 * 1024 * 1024)
            return Error(400, "Provide plan inputs and a previous result of at most 25 MB each.");
        var owner = Owner(user);
        var gate = Gate(input.Id);
        await gate.WaitAsync();
        var submissionStarted = false;
        try
        {
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
            var existing = Read(owner, input.Id);
            if (existing is not null)
                return existing.Fingerprint != fingerprint ? Error(409, "This session already belongs to different plan inputs.") : Results.Ok(View(existing));
            var prepared = PlanningRequest.Prepare(input);
            // Persist before submit; an uncertain POST is never automatically repeated.
            var saved = new Saved(input.Id, owner, fingerprint, Path.GetFileName(input.FileName), input.RequestJson, prepared, DateTimeOffset.UtcNow, WorkspaceDraftId: input.WorkspaceDraftId);
            Save(saved);
            using var client = new PtvClient(_settings);
            // Independent of browser disconnect: retain the accepted provider ID for recovery.
            submissionStarted = true;
            var response = await client.SubmitAsync(ManifestReportExtractor.RemoveReportingSidecar(prepared), CancellationToken.None);
            if (!response.IsSuccess)
            {
                saved = saved with { Status = "FAILED" };
                Save(saved);
                return Error(502, "PTV rejected the planning request.", response.Body);
            }
            var id = JsonNode.Parse(response.Body)?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id)) return Error(502, "PTV accepted the request without a job reference. Do not resubmit automatically.");
            saved = saved with { ProviderId = id, Status = "QUEUING" };
            Save(saved);
            return Results.Ok(View(saved));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or ArgumentException)
        { return submissionStarted ? Error(502, "Submission could not be confirmed. Reconnect to this session before starting another plan.") : Error(400, "The plan contains invalid or conflicting settings.", ex.Message); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return Error(502, "Submission could not be confirmed. Reconnect to this session before starting another plan."); }
        finally { gate.Release(); }
    }
    public async Task<IResult> Poll(Guid id, ClaimsPrincipal user, bool stop, CancellationToken ct, bool includeInputs = false)
    {
        var owner = Owner(user);
        var gate = Gate(id);
        await gate.WaitAsync(ct);
        try
        {
            var saved = Read(owner, id);
            if (saved is null) return Error(404, "This planning session is unavailable or expired. Downloaded inputs can be imported into a new plan.");
            if (saved.ProviderId is null) return Results.Ok(View(saved, warning: saved.Status == "FAILED" ? "PTV rejected this plan. Review the inputs before starting a new run." : "Submission is unconfirmed. It may still be running at PTV. Contact support before submitting the same plan again.", includeInputs: includeInputs));
            if (PtvClient.IsTerminal(saved.Status)) return Results.Ok(View(saved, includeInputs: includeInputs));
            using var client = new PtvClient(_settings);
            if (stop && saved.Status != "STOPPING")
            {
                var stopped = await client.StopAsync(saved.ProviderId, ct);
                if (!stopped.IsSuccess) return Error(502, "PTV did not confirm the stop. Keep monitoring and retry.", stopped.Body);
                saved = saved with { Status = "STOPPING" };
                Save(saved);
                return Results.Ok(View(saved));
            }
            var response = await client.ResultAsync(saved.ProviderId, ct);
            if (!response.IsSuccess) return Error(502, "Could not refresh the plan. The existing PTV job has not been resubmitted.", response.Body);
            var body = JsonNode.Parse(response.Body)!;
            var status = body["status"]?.GetValue<string>() ?? "UNKNOWN";
            var result = status == "SUCCEEDED" ? OptimizationService.Describe(saved.FileName, saved.PreparedJson,
                new PtvRun(saved.ProviderId, status, response, DateTimeOffset.UtcNow - saved.Started)) : null;
            var samples = new List<PlanningSampleDto>();
            string? warning = null;
            if (body["metrics"] is JsonObject current) samples.Add(Sample(DateTimeOffset.UtcNow, current));
            if (!PtvClient.IsTerminal(status))
            {
                try
                {
                    var progress = await client.ProgressAsync(saved.ProviderId, ct);
                    if (progress.IsSuccess && JsonNode.Parse(progress.Body)?["samples"] is JsonArray history)
                    {
                        var parsed = history.OfType<JsonObject>().Where(s => s["metrics"] is JsonObject)
                            .Select(s => Sample(s["time"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.UtcNow, s["metrics"]!.AsObject())).ToList();
                        if (parsed.Count > 0) samples = parsed;
                    }
                    else warning = "Progress history is unavailable; showing the latest result metrics.";
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
                { warning = "Progress history is temporarily unavailable; the optimization is still being monitored."; }
            }
            if (status == "FAILED") warning = body["error"]?["description"]?.GetValue<string>() ?? "PTV could not complete this plan. Your previous result is unchanged.";
            saved = saved with { Status = status, Result = result };
            if (saved.Status != Read(owner, id)?.Status || result is not null) Save(saved);
            return Results.Ok(View(saved, samples, warning, includeInputs));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { return Error(502, "The planning update could not be retrieved. Reconnect to continue monitoring the same job."); }
        finally { gate.Release(); }
    }
    public async Task<OptimizationResultDto> Completed(Guid id, Guid draftId, ClaimsPrincipal user, CancellationToken ct)
    {
        var gate = Gate(id);
        await gate.WaitAsync(ct);
        try
        {
            var saved = Read(Owner(user), id);
            if (saved?.WorkspaceDraftId != draftId || saved.Status != "SUCCEEDED" || saved.Result is null)
                throw new FormatException("A completed optimization belonging to this selection is required. Reconnect to the wizard first.");
            return saved.Result;
        }
        finally { gate.Release(); }
    }
    public static PlanningSampleDto Sample(DateTimeOffset time, JsonObject metrics) => new(time,
        metrics["numberOfScheduledOrders"]?.GetValue<int>() ?? 0, metrics["numberOfUnscheduledOrders"]?.GetValue<int>() ?? 0,
        metrics["numberOfRoutes"]?.GetValue<int>() ?? 0,
        metrics["costs"]?["grossTotal"]?.GetValue<double>() ?? metrics["totalCost"]?.GetValue<double>());
    private static PlanningSessionDto View(Saved s, IReadOnlyList<PlanningSampleDto>? samples = null, string? warning = null, bool includeInputs = false) =>
        new(s.Id, s.Status, s.FileName, includeInputs ? s.RequestJson : "", s.Result?.ElapsedSeconds ?? (DateTimeOffset.UtcNow - s.Started).TotalSeconds, samples ?? [], s.Result, warning);
    private static IResult Error(int status, string message, string? detail = null) => Results.Json(new ApiErrorDto(message, detail is null ? null : [detail]), statusCode: status);
}
