using System.Diagnostics;
using System.Security.Claims;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Dapper;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace Aurora.Api.Features.Auth;

public sealed class AppLog(NpgsqlDataSource dataSource, ITenantContext tenant, ILogger<AppLog> logger)
{
    public const string ActorKey = "AppLog.Actor";
    public const string DetailKey = "AppLog.Detail";
    public const string ClientErrorKey = "AppLog.ClientError";
    public const string ReferencesKey = "AppLog.References";
    public const string ActionKey = "AppLog.Action";
    private const string ReferencesMarker = " · References: ";
    public sealed record Actor(Guid Id, string Name);
    private sealed record LogTenant(Guid? TenantIdOrNull, Guid? UserIdOrNull) : ITenantContext;

    // Never store request bodies, query strings, passwords, tokens or raw exception messages.
    public static string Resource(HttpContext context)
    {
        var resource = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "Unmatched endpoint";
        if (context.Request.RouteValues.TryGetValue("kind", out var kind) && kind?.ToString() is "fleet" or "orders" or "terminals" or "customers")
            resource = resource.Replace("{kind}", kind.ToString());
        return resource;
    }
    public static string SafeDetail(string value)
    {
        value = System.Text.RegularExpressions.Regex.Replace(value, @"(?i)(password|pwd|token|secret|authorization|connectionstring)\s*[:=]\s*[^;\s,]+", "$1=[redacted]");
        return value.Length > 1000 ? value[..1000] : value;
    }
    public static string Describe(string path, string method, int status)
    {
        string action;
        if(path=="/api/auth/nova/token")return status<400?"Requested Nova launch":"Nova launch request rejected";
        if(path is "/api/auth/nova/redeem" or "/api/auth/nova-launch")return status<400?"Signed in from Nova":"Nova sign-in rejected";
        if (path.Contains("/auth/login")) return status < 400 ? "Logged in" : "Login failed";
        if (path.Contains("/auth/logout")) return "Logged out";
        if (path.Contains("/sessions") || path.Contains("/optimizations"))
            action = path.Contains("/accept/") ? "Accepted optimization plan" : path.EndsWith("/stop") ? "Stopped optimization" : "Ran optimization";
        else if (path.EndsWith("/finish")) action = "Created manifests";
        else if (path.EndsWith("/dispatch") && method == "POST") action = "Dispatched manifest";
        else if (path.EndsWith("/assignment") && method == "PUT") action = "Assigned manifest";
        else if (path.Contains("/drafts")) action = path.EndsWith("/cancel") ? "Cancelled plan" : "Started plan";
        else if (path.Contains("/client-error")) action = "Application error";
        else
        {
            var noun=path.Contains("/fleet") ? "truck" : path.Contains("/terminals") ? "transportation node" : path.Contains("/customers") ? "customer"
                : path.Contains("/orders") ? "order" : path.Contains("/manifests") ? "manifest" : path.Contains("/users") ? "user" : path.Contains("/databases") ? "database" : "record";
            action=(method switch { "POST"=>"Added ", "PUT" or "PATCH"=>"Edited ", "DELETE"=>"Deleted ", _=>"Viewed " })+noun;
        }
        return status>=400 ? action+" — failed" : action;
    }
    public static string Action(HttpContext context) => Describe(context.Request.Path.Value ?? "", context.Request.Method,context.Response.StatusCode);
    public static bool IsUserAction(HttpContext context) => context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE"
        && !context.Request.Path.StartsWithSegments("/connect")
        && !context.Request.Path.StartsWithSegments("/api/v1/admin/applog")
        && !context.Request.Path.Value!.EndsWith("/refresh")
        && !context.Request.Path.Value!.EndsWith("/road-route");
    private static string DisplayAction(AppLogEntry entry) => entry.Action switch
    {
        _ when entry.Resource is "/api/auth/nova-launch" or "/api/auth/nova/redeem" or "/api/auth/nova/token"=>Describe(entry.Resource,"POST",entry.StatusCode),
        "Sign in"=>"Logged in", "Sign out"=>"Logged out", "Sign-in failed"=>"Login failed",
        _ when entry.Action.StartsWith("Create / run")=>Describe(entry.Resource,"POST",entry.StatusCode),
        _ when entry.Action.StartsWith("Update")=>Describe(entry.Resource,"PUT",entry.StatusCode),
        _ when entry.Action.StartsWith("Delete")=>Describe(entry.Resource,"DELETE",entry.StatusCode),
        _=>entry.Action
    };
    public static AppLogEntry ForDisplay(AppLogEntry entry)
    {
        var marker = entry.Detail.LastIndexOf(ReferencesMarker, StringComparison.Ordinal);
        var references = marker >= 0 ? entry.Detail[(marker + ReferencesMarker.Length)..] : "";
        // Legacy route IDs are business numbers only for native Nova manifests.
        if (marker < 0 && entry.Resource.Contains("/firebird/manifests/{id"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(entry.Detail, @"\bRecord (\d+)$");
            if (match.Success) references = "Manifest " + match.Groups[1].Value;
        }
        return entry with { Action=DisplayAction(entry), References=references };
    }
    public static string BusinessReferences(string path, object? value)
    {
        static string Ref(string label, string? number) => string.IsNullOrWhiteSpace(number) ? "" : label + " " + SafeDetail(number.Trim()).Replace("\r", " ").Replace("\n", " ");
        static string Field(NovaRecordDocument doc, string key) => doc.Parts.SelectMany(p=>p.Fields).FirstOrDefault(f=>f.Key==key)?.Value ?? "";
        return value switch
        {
            NovaDispatchDocument doc => Ref("Manifest", doc.ManifestId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            long number when path.EndsWith("/firebird/manifests") => Ref("Manifest", number.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            IEnumerable<SavedManifestDto> manifests => string.Join("; ", manifests.Select(m=>Ref("Manifest", m.Number)).Where(s=>s.Length>0).Distinct()),
            SavedManifestDto manifest => Ref("Manifest", manifest.Number),
            NovaRecordDocument doc when path.Contains("/records/orders") => Ref("Order/PRO", Field(doc,"PBNUM")),
            NovaRecordDocument doc when path.Contains("/records/fleet") => Ref("Truck", Field(doc,"EH_UNIT_ID")),
            NovaMasterDocument doc when path.Contains("/masters/customers") => Ref("Customer", doc.Id),
            NovaMasterDocument doc when path.Contains("/masters/terminals") => Ref("Terminal", doc.Id),
            _ => ""
        };
    }
    public async Task Write(HttpContext context, Exception? error, long duration)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var actor = context.Items[ActorKey] as Actor;
            var userId = actor?.Id ?? tenant.UserIdOrNull;
            var tenantId = tenant.TenantIdOrNull;
            var name = actor?.Name ?? context.User.Identity?.Name;
            if (userId is {} uid)
            {
                await using var lookup = await new NpgsqlConnectionFactory(dataSource, new LogTenant(tenantId, uid)).OpenConnectionAsync(timeout.Token);
                tenantId ??= await lookup.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT tenant_id FROM user_tenant WHERE user_id=@uid AND is_default", new { uid }, cancellationToken: timeout.Token));
                name ??= await lookup.QuerySingleOrDefaultAsync<string>(new CommandDefinition("SELECT \"UserName\" FROM \"AspNetUsers\" WHERE \"Id\"=@uid", new { uid }, cancellationToken: timeout.Token));
            }
            // Unattributed security events are intentionally generic and contain no supplied username or record ID.
            var status = error is null ? context.Response.StatusCode : 500;
            var level = status is 401 or 403 ? "Warning" : status >= 400 ? "Error" : "Information";
            var action = error is null && status < 400 && context.Items[ActionKey] is string namedAction ? namedAction : Action(context);
            if (context.Items.ContainsKey(ClientErrorKey)) { level = "Error"; action = "Client error"; }
            if (error is not null && context.Request.Path == "/api/auth/login") action = "Sign-in failed";
            var detail = error is null ? context.Items[DetailKey] as string ?? $"HTTP {status}" : $"Unhandled {error.GetType().Name}";
            if (userId is not null && context.Request.RouteValues.TryGetValue("id", out var id)) detail += $" · Record {id}";
            if (userId is not null && context.Items[ReferencesKey] is string references && references.Length > 0)
                detail += ReferencesMarker + references;
            await using var db = await new NpgsqlConnectionFactory(dataSource, new LogTenant(tenantId, userId)).OpenConnectionAsync(timeout.Token);
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO applog(tenant_id,user_id,user_name,level,action,resource,status_code,detail,trace_id,duration_ms)
                VALUES(@tenantId,@userId,@name,@level,@action,@resource,@status,@detail,@trace,@duration)
                """, new { tenantId, userId, name = name ?? "Anonymous", level, action, resource = Resource(context), status,
                    detail, trace = context.TraceIdentifier, duration }, cancellationToken: timeout.Token));
        }
        catch (Exception ex)
        {
            // Audit storage failure must not turn a completed business operation into a failed request.
            logger.LogError("Application log write failed ({ErrorType}); request {TraceId}", ex.GetType().Name, context.TraceIdentifier);
        }
    }
}

public sealed class AppLogMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppLog log)
    {
        if (!context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/connect")) { await next(context); return; }
        var watch = Stopwatch.StartNew();
        Exception? failure = null;
        try { await next(context); }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            var failed = failure is not null || context.Response.StatusCode >= 400;
            var logViewer = context.Request.Path.StartsWithSegments("/api/v1/admin/applog") || context.Request.Path.StartsWithSegments("/api/v1/me")
                || context.Request.Path == "/connect/userinfo" || context.Request.Path == "/connect/token";
            if (failed || (!logViewer && AppLog.IsUserAction(context) && (context.User.Identity?.IsAuthenticated == true || context.Items.ContainsKey(AppLog.ActorKey)
                || context.Request.Path == "/api/auth/login" || context.Request.Path == "/api/auth/logout")))
                await log.Write(context, failure, watch.ElapsedMilliseconds);
        }
    }
}

public static class AppLogEndpoints
{
    public static void MapAppLogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/can-view-applog", (HttpContext context) => Results.Ok(context.User.IsInRole("aurora:Admin") || context.User.IsInRole("aurora:PowerUser"))).RequireAuthorization();
        app.MapPost("/api/v1/applog/client-error", (ClientLogError input, HttpContext context) =>
        {
            // Accept only event categories and known screen names, never arbitrary browser text or URLs.
            if (input.Kind is not ("UI failure" or "Network failure") || input.Page is null || input.Page.Length > 40) return Results.BadRequest();
            var page = input.Page is "fleet" or "terminals" or "customers" or "orders" or "manifests" or "route-optimization" or "planning" or "users" or "databases" or "applog" ? input.Page : "Application";
            context.Items[AppLog.ClientErrorKey] = true;
            context.Items[AppLog.DetailKey] = $"{input.Kind} · {page}";
            return Results.NoContent();
        }).RequireAuthorization();
        app.MapPost("/api/v1/admin/applog/clear", async (NpgsqlConnectionFactory factory, ITenantContext tenant, CancellationToken ct) =>
        {
            await using var db=await factory.OpenConnectionAsync(ct);
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO applog_view(tenant_id,user_id,cleared_before) VALUES(@TenantId,@UserId,clock_timestamp()) ON CONFLICT(tenant_id,user_id) DO UPDATE SET cleared_before=excluded.cleared_before",new {tenant.TenantId,tenant.UserId},cancellationToken:ct));
            return Results.NoContent();
        }).RequireAuthorization(p=>p.RequireRole("aurora:Admin","aurora:PowerUser"));
        app.MapGet("/api/v1/admin/applog", async (string? search, string? level, int? page, NpgsqlConnectionFactory factory, CancellationToken ct) =>
        {
            if ((page ?? 0) < 0 || page > 100000 || search?.Length > 200 || (level is not (null or "" or "Information" or "Warning" or "Error"))) return Results.BadRequest(new ApiErrorDto("Invalid log filter."));
            await using var db = await factory.OpenConnectionAsync(ct);
            var records = (await db.QueryAsync<AppLogEntry>(new CommandDefinition("""
                SELECT id,occurred_utc AS OccurredUtc,user_name AS UserName,level,action,resource,status_code AS StatusCode,detail,trace_id AS TraceId,duration_ms AS DurationMs
                FROM applog WHERE (@level='' OR level=@level)
                AND action NOT LIKE 'View%'
                AND resource NOT LIKE '%/firebird/refresh'
                AND occurred_utc > COALESCE((SELECT cleared_before FROM applog_view LIMIT 1),'-infinity'::timestamptz)
                AND (@search='' OR strpos(lower(user_name || ' ' || action || ' ' || resource || ' ' || detail || ' ' || trace_id),lower(@search))>0)
                ORDER BY occurred_utc DESC,id DESC LIMIT 51 OFFSET @offset
                """, new { level = level ?? "", search = search?.Trim() ?? "", offset = (page ?? 0) * 50 }, cancellationToken: ct))).ToArray();
            return Results.Ok(new AppLogPage(records.Take(50).Select(AppLog.ForDisplay).ToArray(), records.Length > 50));
        }).RequireAuthorization(p => p.RequireRole("aurora:Admin", "aurora:PowerUser"));
    }
}

public sealed class AppLogResultFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        if (result is IValueHttpResult { Value: ApiErrorDto error }) context.HttpContext.Items[AppLog.DetailKey] = AppLog.SafeDetail(error.Error);
        else if (AppLog.IsUserAction(context.HttpContext) && result is IValueHttpResult value
            && (result is not IStatusCodeHttpResult status || status.StatusCode is null or < 400))
        {
            var references = AppLog.BusinessReferences(context.HttpContext.Request.Path.Value ?? "", value.Value);
            if (references.Length > 0) context.HttpContext.Items[AppLog.ReferencesKey] = references;
        }
        return result;
    }
}
