using Aurora.Api.Features.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Aurora.Modules.Routing;
using Dapper;
using Npgsql;

var checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);checks++;}
foreach(var path in new[]{"/api/v1/tms/orders","/api/v1/tms/manifests","/api/v1/tms/drivers","/api/v1/tms/customers","/api/v1/tms/terminals","/api/v1/tms/orders/import","/api/v1/tms/orders/a/b/workflow/events","/api/v1/routing/equipment/units","/api/v1/routing/equipment/types","/api/v1/aurora/manifests/00000000-0000-0000-0000-000000000001/freightops"})
    foreach(var verb in new[]{"POST","PUT","DELETE"})Check(NativeWorkspaceFilter.Blocks(verb,path),$"{verb} {path} cannot write local records in native mode");
foreach(var path in new[]{"/api/v1/tms/orders","/api/v1/routing/equipment","/api/v1/tms/terminals"})Check(!NativeWorkspaceFilter.Blocks("GET",path),"Planning read remains available: "+path);
foreach(var path in new[]{"/api/v1/routing/sessions","/api/v1/aurora/firebird/records/orders","/api/v1/tms/orders/lookup-address","/api/v1/tms/orders/convert-document","/api/v1/tms/orders/import/check"})Check(!NativeWorkspaceFilter.Blocks("POST",path),"Native/planning/non-save operation remains available: "+path);
foreach(var local in new[]{true,false})
{
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Synchronization:LocalWorkspace",local.ToString()}}).Build();
    var http=new DefaultHttpContext();http.Request.Method="POST";http.Request.Path="/api/v1/tms/orders";
    var called=false;
    var result=await new NativeWorkspaceFilter(config).InvokeAsync(new Invocation(http),_=>{called=true;return ValueTask.FromResult<object?>("saved");});
    Check(called==local,"Endpoint executes only in local workspace mode: "+local);
    if(!local)Check(result is IStatusCodeHttpResult {StatusCode:409},"Stale local save returns a visible conflict");
}
if(Environment.GetEnvironmentVariable("NATIVE_WORKSPACE_TEST_CONNECTION") is {Length:>0} connection)
{
    var config=new NpgsqlConnectionStringBuilder(connection);
    if(config.Host is not ("localhost" or "127.0.0.1")||config.Database?.StartsWith("aurora_native_check_",StringComparison.Ordinal)!=true)throw new Exception("Use a disposable local test database.");
    await using var db=new NpgsqlConnection(connection);await db.OpenAsync();
    await db.ExecuteAsync("""
        CREATE TABLE aurora_order_source(tenant_id uuid,id uuid,name text,request jsonb,PRIMARY KEY(tenant_id,id),UNIQUE(tenant_id,name));
        CREATE TABLE aurora_sync_connection(tenant_id uuid,provider text,source_identity text);
        CREATE TABLE aurora_order(tenant_id uuid,source_id uuid,details jsonb);
        """);
    await db.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"0047_integration_staging.sql")));
    var tenant=Guid.NewGuid();var other=Guid.NewGuid();var native=Guid.NewGuid();
    await db.ExecuteAsync("""
        INSERT INTO aurora_sync_connection VALUES(@tenant,'nova','native-test');
        INSERT INTO aurora_order_source VALUES(@tenant,@native,'Native planning','{"reporting":{"firebirdSource":"native-test"}}');
        INSERT INTO aurora_order VALUES(@tenant,@native,'{"TerminalCode":"TPA"}');
        """,new{tenant,native});
    var cache=await WorkspaceOrderSource.Get(db,tenant,"TPA",null,default,true);
    Check(cache!=native,"Transport source never adopts native planner snapshots");
    Check(await WorkspaceOrderSource.Get(db,tenant,"TPA",null,default,true)==cache,"Repeated imports use the same transport source");
    Check(await WorkspaceOrderSource.Get(db,other,"TPA",null,default,true)!=cache,"Each tenant gets an isolated transport source");
    Check(!await db.ExecuteScalarAsync<bool>("SELECT aurora_source_in_workspace(@tenant,@cache)",new{tenant,cache}),"Integration cache cannot be selected for planning");
    Check(await db.ExecuteScalarAsync<bool>("SELECT aurora_source_in_workspace(@tenant,@native)",new{tenant,native}),"Native planning source remains visible");
    Check(await WorkspaceOrderSource.Get(db,tenant,"TPA",null,default)==native,"Local mode retains existing adoption behavior");
    await db.ExecuteAsync("UPDATE aurora_sync_connection SET source_identity='other-database' WHERE tenant_id=@tenant",new{tenant});
    Check(!await db.ExecuteScalarAsync<bool>("SELECT aurora_source_in_workspace(@tenant,@native)",new{tenant,native}),"Old database planning snapshots remain isolated");
    await AuthorityChecks.Run(connection,Check);
}
Console.WriteLine($"{checks} native workspace checks passed.");

sealed class Invocation(HttpContext http):EndpointFilterInvocationContext
{
    public override HttpContext HttpContext=>http;
    public override IList<object?> Arguments=>Array.Empty<object?>();
    public override T GetArgument<T>(int index)=>throw new ArgumentOutOfRangeException(nameof(index));
}
