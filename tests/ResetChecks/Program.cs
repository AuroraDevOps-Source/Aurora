using Aurora.Integrations.Nova;
using System.Security.Claims;
using System.Text.Json;
using Aurora.Api.Features.Routing;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

var count = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
var tenant = new TestTenant(Guid.NewGuid());
Check(FirebirdRoutingSource.IsAuroraManifest(FirebirdRoutingSource.ExportKey(tenant.Id, Guid.NewGuid(), "TRUCK"), tenant.Id), "optimized manifest belongs to company");
Check(FirebirdRoutingSource.IsAuroraManifest($"aurora-manual:{tenant.Id:N}:{Guid.NewGuid():N}", tenant.Id), "manual manifest belongs to company");
Check(!FirebirdRoutingSource.IsAuroraManifest("", tenant.Id), "pre-existing unmarked manifest is preserved");
Check(!FirebirdRoutingSource.IsAuroraManifest(FirebirdRoutingSource.ExportKey(Guid.NewGuid(), Guid.NewGuid(), "TRUCK"), tenant.Id), "another company's manifest is preserved");
var builder = WebApplication.CreateBuilder(); builder.Services.AddAuthorization(); builder.Services.AddScoped<WorkspaceResetStore>(); builder.Services.AddSingleton<PlanningSessions>();
await using var app = builder.Build(); app.MapWorkspaceResetEndpoints();
var policies = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
var authorization = app.Services.GetRequiredService<IAuthorizationService>();
foreach (var endpoint in ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>())
{
    var policy = (await AuthorizationPolicy.CombineAsync(policies, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()))!;
    foreach (var role in new[] { "aurora:Admin", "aurora:PowerUser", "aurora:User", "other:Admin", "" })
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(role.Length == 0 ? [] : [new Claim(ClaimTypes.Role, role)], role.Length == 0 ? null : "test"));
        Check((await authorization.AuthorizeAsync(user, null, policy)).Succeeded == (role == "aurora:Admin"), "reset endpoint protects role " + role);
    }
}
if (Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG") is { } path)
{
    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))!;
    var cs = new FbConnectionStringBuilder(config["FirebirdRouting:ConnectionString"].GetString());
    if (cs.DataSource != "172.23.0.5" || !cs.Database.Equals(@"D:\db\wayl_test\operations.fdb", StringComparison.OrdinalIgnoreCase)) throw new Exception("Only WayTest rollback checks are allowed.");
    await using var pg = NpgsqlDataSource.Create(config["ConnectionStrings:Aurora"].GetString()!);
    var source = new FirebirdRoutingSource(new NpgsqlConnectionFactory(pg, tenant), tenant, Options.Create(new FirebirdRoutingOptions { Enabled = true, TenantId = tenant.Id, ConnectionString = cs.ConnectionString, SourceKey = "reset-checks" }));
    await using var db = new FbConnection(cs.ConnectionString); await db.OpenAsync();
    await using var tx = db.BeginTransaction();
    const long manifest = 1990980001, order = 1990980002, route = 1990980003, stop = 1990980004, preserved = 1990980005;
    async Task Exec(string sql, object? args = null) => _ = await db.ExecuteAsync(sql, args, tx);
    async Task<T> Scalar<T>(string sql) => (await db.ExecuteScalarAsync<T>(sql, transaction: tx))!;
    try
    {
        Check(await Scalar<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID BETWEEN 1990980001 AND 1990980005") == 0, "synthetic IDs are unused");
        await Exec("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_EXTERNAL_REF) VALUES(@manifest,'M','M','MHT','MHT','MHT',1,CURRENT_TIMESTAMP,@reference)", new { manifest, reference = FirebirdRoutingSource.ExportKey(tenant.Id, Guid.NewGuid(), "TRUCK") });
        await Exec("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PB_STATUS,PB_DT_ENT) VALUES(@order,'O','O','MHT',110,CURRENT_TIMESTAMP)", new { order });
        await Exec("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT) VALUES(@preserved,'M','M','MHT','MHT','MHT',1,CURRENT_TIMESTAMP)", new { preserved });
        await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION) VALUES(@route,@manifest,1,'L','MHT')", new { route, manifest });
        await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_TYPE,OPS_MANIFEST_NUMBER,OPS_DELIVERED_TIME) VALUES(@stop,@order,2,'D',@manifest,CURRENT_TIMESTAMP)", new { stop, order, manifest });
        await Exec("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_SEQUENCE,OSM_PKUP_DELV_ID) VALUES(@route,1,@stop)", new { route, stop });
        try { await source.WriteResetManifests(db, tx, [preserved], 110, default); throw new Exception("unmarked manifest accepted"); }
        catch (FormatException) { Check(true, "native reset refuses a pre-existing manifest"); }
        Check(await source.WriteResetManifests(db, tx, [manifest], 110, default) == 1, "native reset returns the member order");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID={manifest}") == 0, "Aurora manifest is deleted");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID={preserved}") == 1, "pre-existing manifest remains");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID={manifest}") == 0, "manifest routing stops are deleted by native trigger");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID={route}") == 0, "manifest membership removed");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_STOP_ID={stop} AND OPS_MANIFEST_NUMBER IS NULL AND OPS_DELIVERED_TIME IS NULL AND OPS_STATUS=110") == 1, "source delivery remains and is reset for routing");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID={order} AND PB_STATUS=110") == 1, "order header ready status restored");
        Check(await source.WriteResetManifests(db, tx, [manifest], 110, default) == 0, "native retry is idempotent");
    }
    finally { await tx.RollbackAsync(); }
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID BETWEEN 1990980001 AND 1990980005") == 0, "all synthetic writes rolled back");
}
if (Environment.GetEnvironmentVariable("RESET_CHECK_CONNECTION") is { } connection)
{
    var settings = new NpgsqlConnectionStringBuilder(connection);
    if (settings.Host != "127.0.0.1" || settings.Port != 55439 || !settings.Database!.StartsWith("aurora_workspace_checks_reset_")) throw new Exception("Only isolated reset-check databases are allowed.");
    await using var data = NpgsqlDataSource.Create(connection);
    var factory = new NpgsqlConnectionFactory(data, tenant);
    var source = new FirebirdRoutingSource(factory, tenant, Options.Create(new FirebirdRoutingOptions()));
    var store = new WorkspaceResetStore(factory, tenant, source);
    var foreign = Guid.NewGuid(); var sourceId = Guid.NewGuid(); var draft = Guid.NewGuid(); var manifest = Guid.NewGuid();
    await using var db = await factory.OpenConnectionAsync();
    foreach (var id in new[] { tenant.Id, foreign })
    {
        await db.ExecuteAsync("INSERT INTO tenant(id,name,slug) VALUES(@id,'Reset fixture',@slug)", new { id, slug = id.ToString() });
        await db.ExecuteAsync("INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES(@id,@sourceId,'Fixture','{}')", new { id, sourceId });
        await db.ExecuteAsync("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,status) VALUES(@id,@sourceId,'TEST',now(),'Keep customer','Keep city','Routed')", new { id, sourceId });
        await db.ExecuteAsync("INSERT INTO aurora_planning_draft(tenant_id,id,owner_id,source_id,order_ids,request) VALUES(@id,@draft,'owner',@sourceId,ARRAY['TEST'],'{}')", new { id, draft, sourceId });
        await db.ExecuteAsync("INSERT INTO aurora_manifest(tenant_id,id,draft_id,vehicle,data) VALUES(@id,@manifest,@draft,'Keep truck','{}')", new { id, manifest, draft });
        await db.ExecuteAsync("INSERT INTO aurora_manifest_order(tenant_id,manifest_id,source_id,order_id,stop_number) VALUES(@id,@manifest,@sourceId,'TEST',1)", new { id, manifest, sourceId });
        await db.ExecuteAsync("INSERT INTO aurora_freight_workflow(tenant_id,source_id,order_id,data) VALUES(@id,@sourceId,'TEST','{\"Events\":[{}]}')", new { id, sourceId });
    }
    var preview = await store.Preview(default);
    Check(preview.Manifests == 1 && preview.Drafts == 1 && preview.Orders == 1, "preview counts only current company");
    try { await store.Execute(new(preview.Revision, ""), default); throw new Exception("confirmation missing"); }
    catch (FormatException) { Check(true, "reset requires explicit confirmation"); }
    try { await store.Execute(new("stale", "RESET"), default); throw new Exception("stale preview accepted"); }
    catch (FormatException) { Check(true, "reset rejects changed preview"); }
    Check((await store.Preview(default)).Revision == preview.Revision, "rejected reset preserves all records");
    var reset = await store.Execute(new(preview.Revision, "RESET"), default);
    Check(reset.Result.Manifests == 1 && reset.Drafts.SequenceEqual([draft]), "reset returns removed drafts for session cleanup");
    Check((await store.Preview(default)) is { Manifests: 0, Drafts: 0, Orders: 1 }, "reset clears manifests and drafts but retains orders");
    Check(await db.ExecuteScalarAsync<string>("SELECT status FROM aurora_order WHERE tenant_id=@id", new { id = tenant.Id }) == WorkspaceOrderStatus.Ready, "local orders return to Ready to Route");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_order_source WHERE tenant_id=@id", new { id = tenant.Id }) == 1, "source configuration preserved");
    Check(await db.ExecuteScalarAsync<bool>("SELECT data='{}'::jsonb AND revision=1 FROM aurora_freight_workflow WHERE tenant_id=@id", new { id = tenant.Id }), "test workflow cleared and stale edits invalidated");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_manifest WHERE tenant_id=@foreign", new { foreign }) == 1, "other company's manifest preserved");
    Check(await db.ExecuteScalarAsync<string>("SELECT status FROM aurora_order WHERE tenant_id=@foreign", new { foreign }) == "Routed", "other company's order state preserved");
    var empty = await store.Preview(default);
    Check((await store.Execute(new(empty.Revision, "RESET"), default)).Result.Manifests == 0, "empty reset can be repeated safely");
}
Console.WriteLine($"{count} reset checks passed.");
sealed record TestTenant(Guid Id) : ITenantContext { public Guid? TenantIdOrNull => Id; public Guid? UserIdOrNull => null; }
