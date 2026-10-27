using Aurora.Integrations.Nova;
using System.Security.Claims;
using Dapper;
using System.Text.Json;
using Aurora.Api.Features.Routing;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using FirebirdSql.Data.FirebirdClient;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
async Task Reject(Func<Task> action, string message) { try { await action(); } catch (Exception e) when (e is FormatException or KeyNotFoundException) { Check(true, message); return; } throw new Exception(message); }
var builder = WebApplication.CreateBuilder();
builder.Services.AddAuthorization();
builder.Services.AddScoped<FirebirdRoutingSource>();
builder.Services.AddScoped<MasterAddressLookup>();
builder.Services.AddScoped<OrderWorkspace>();
builder.Services.AddScoped<PlanningSessions>();
await using var app = builder.Build();
app.MapOrderWorkspaceEndpoints();
var policies = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
var authorization = app.Services.GetRequiredService<IAuthorizationService>();
var deletes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
    .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true).ToArray();
Check(deletes.Length == 3, "truck, terminal and driver deletes exposed");
foreach (var endpoint in deletes)
{
    var policy = (await AuthorizationPolicy.CombineAsync(policies, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()))!;
    foreach (var role in new[] { "aurora:Admin", "aurora:PowerUser", "aurora:User", "other:Admin", "" })
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(role.Length == 0 ? [] : [new Claim(ClaimTypes.Role, role)], role.Length == 0 ? null : "test"));
        var result = await authorization.AuthorizeAsync(user, null, policy);
        Check(result.Succeeded == (role is "aurora:Admin" or "aurora:PowerUser"), $"{endpoint.RoutePattern.RawText}: role '{role}'");
    }
}
// Native terminal deletion is exercised only in a disposable database.
var fixturePath=Path.Combine(Path.GetTempPath(),"aurora-terminal-delete-"+Guid.NewGuid().ToString("N")+".fdb");
var fixtureCs=new FbConnectionStringBuilder{Database=fixturePath,UserID="SYSDBA",Password="test",ServerType=FbServerType.Embedded,ClientLibrary=@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll",Pooling=false}.ConnectionString;
FbConnection.CreateDatabase(fixtureCs);
try
{
    var fixtureTenant=new TestTenant(Guid.NewGuid());
    await using var fixturePg=NpgsqlDataSource.Create("Host=localhost;Database=unused;Username=unused");
    var fixtureSource=new FirebirdRoutingSource(new NpgsqlConnectionFactory(fixturePg,fixtureTenant),fixtureTenant,Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=fixtureTenant.TenantIdOrNull!.Value,ConnectionString=fixtureCs,SourceKey="delete-check"}));
    await using var native=new FbConnection(fixtureCs);await native.OpenAsync();
    var fields=fixtureSource.NewMaster("terminals").Fields;
    await native.ExecuteAsync("CREATE TABLE LOCMAST("+string.Join(',',fields.Select(f=>f.Key+(f.Key=="LM_ID"?" VARCHAR(12) PRIMARY KEY":f.Kind is "latitude" or "longitude"?" DOUBLE PRECISION":" VARCHAR(100)")))+")");
    await native.ExecuteAsync("INSERT INTO LOCMAST(LM_ID,LM_NAME) VALUES('TEST','Test terminal')");
    foreach(var (table,column) in new[]{("EQUIP_HEADER","EH_DOMICILE"),("ACCOUNT","AC_TERM_LOC"),("OPS_HEADER","PB_LOCATION"),("OPS_STOP_REC","OPS_DELIVERY_TERMINAL")})
    {
        await native.ExecuteAsync($"CREATE TABLE {table}({column} VARCHAR(12))");
        await native.ExecuteAsync($"INSERT INTO {table} VALUES('TEST')");
    }
    var terminal=await fixtureSource.ReadMaster("terminals","TEST",default);
    await using(var tx=native.BeginTransaction())
    {
        await Reject(()=>FirebirdRoutingSource.DeleteTerminalRecord(native,tx,"TEST","stale",default),"stale terminal revision rejected");
        await FirebirdRoutingSource.DeleteTerminalRecord(native,tx,"TEST",terminal.Revision,default);
        Check(await native.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LOCMAST",transaction:tx)==0,"in-use terminal can be deleted");
        await tx.RollbackAsync();
    }
    Check((await fixtureSource.ReadMaster("terminals","TEST",default)).Revision==terminal.Revision,"rollback preserves terminal");
    await using(var tx=native.BeginTransaction())
    {
        await FirebirdRoutingSource.DeleteTerminalRecord(native,tx,"TEST",terminal.Revision,default);
        await tx.CommitAsync();
    }
    await Reject(()=>fixtureSource.ReadMaster("terminals","TEST",default),"deleted terminal no longer appears");
    foreach(var (table,column) in new[]{("EQUIP_HEADER","EH_DOMICILE"),("ACCOUNT","AC_TERM_LOC"),("OPS_HEADER","PB_LOCATION"),("OPS_STOP_REC","OPS_DELIVERY_TERMINAL")})
        Check(await native.ExecuteScalarAsync<string>($"SELECT {column} FROM {table}")=="TEST",$"{table} record and terminal reference preserved");
}
finally{FbConnection.ClearAllPools();FbConnection.DropDatabase(fixtureCs);}
var path = Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG");
if (path is not null)
{
    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))!.ToDictionary(p => p.Key, p => p.Value.ToString());
    var fb = new FbConnectionStringBuilder(config["FirebirdRouting:ConnectionString"]);
    if (fb.DataSource != "172.23.0.5" || !fb.Database.Equals(@"D:\db\wayl_test\operations.fdb", StringComparison.OrdinalIgnoreCase)) throw new Exception("Only WayTest is allowed.");
    var tenant = new TestTenant(Guid.Parse(config["FirebirdRouting:TenantId"]));
    await using var pg = NpgsqlDataSource.Create(config["ConnectionStrings:Aurora"]);
    var source = new FirebirdRoutingSource(new NpgsqlConnectionFactory(pg, tenant), tenant, Options.Create(new FirebirdRoutingOptions { Enabled = true, TenantId = tenant.TenantIdOrNull!.Value, ConnectionString = fb.ConnectionString, SourceKey = config["FirebirdRouting:SourceKey"] }));
    var rows = await source.RecordList("fleet", "TEST-002", 0, default);
    var truck = await source.ReadRecord("fleet", rows.Records.Single().Id, default);
    await Reject(() => source.DeleteTruck(truck.Id, "stale", default, true), "stale truck deletion rejected");
    await source.DeleteTruck(truck.Id, truck.Revision, default, true);
    Check((await source.ReadRecord("fleet", truck.Id, default)).Revision == truck.Revision, "truck delete SQL succeeds and rollback preserves truck");
    var mht = await source.ReadMaster("terminals", "MHT", default);
    await source.DeleteTransportationNode("MHT", mht.Revision, default, true);
    Check((await source.ReadMaster("terminals", "MHT", default)).Revision==mht.Revision,"referenced terminal delete succeeds and rollback preserves it");
    var code = "ZDEL" + Guid.NewGuid().ToString("N")[..7];
    var fields = source.NewMaster("terminals").Fields.ToDictionary(f => f.Key, f => (string?)null);
    fields["LM_ID"] = code; fields["LM_NAME"] = "Temporary delete check";
    var node = await source.CreateMaster("terminals", new("", fields), default);
    try
    {
        await Reject(() => source.DeleteTransportationNode(code, "stale", default, true), "stale node deletion rejected");
        await source.DeleteTransportationNode(code, node.Revision, default, true);
        Check((await source.ReadMaster("terminals", code, default)).Revision == node.Revision, "node delete SQL succeeds and rollback preserves node");
    }
    finally { await source.DeleteTransportationNode(code, node.Revision, default); }
    await Reject(() => source.ReadMaster("terminals", code, default), "temporary node deletion committed and verified");
    Check((await source.RecordList("fleet", null, 0, default)).Records.Length == 10, "all ten user trucks preserved");
}
Console.WriteLine($"PASS {checks} deletion checks.");
sealed class TestTenant(Guid id) : ITenantContext { public Guid? TenantIdOrNull => id; public Guid? UserIdOrNull => null; }
