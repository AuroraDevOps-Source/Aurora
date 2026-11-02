using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Aurora.Integrations.FreightOps;
using Dapper;
using Npgsql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// This suite installs only sync schema in a NEW local disposable database.
var raw=Environment.GetEnvironmentVariable("SYNC_FOUNDATION_TEST_CONNECTION")
    ??throw new Exception("Set SYNC_FOUNDATION_TEST_CONNECTION to a new empty local aurora_sync_check_* database.");
var builder=new NpgsqlConnectionStringBuilder(raw);
if(builder.Host is not ("127.0.0.1" or "localhost") || builder.Database?.StartsWith("aurora_sync_check_",StringComparison.Ordinal)!=true)
    throw new Exception("Only a local disposable sync database is allowed.");
int count=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
var tenant=Guid.NewGuid();var other=Guid.NewGuid();var nova=Guid.NewGuid();var freight=Guid.NewGuid();
await using(var setup=new NpgsqlConnection(raw))
{
    await setup.OpenAsync();
    if(await setup.ExecuteScalarAsync<int>("SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')")!=0)
        throw new Exception("Database is not empty; refusing schema setup.");
    await setup.ExecuteAsync("""
        CREATE TABLE tenant(id uuid PRIMARY KEY);
        GRANT SELECT,INSERT ON tenant TO aurora_app;
        CREATE TABLE aurora_order_source(tenant_id uuid,id uuid,request jsonb);
        GRANT SELECT,INSERT ON aurora_order_source TO aurora_app;
        CREATE TABLE sync_fixture(tenant_id uuid NOT NULL,id text NOT NULL,revision bigint NOT NULL DEFAULT 0,data jsonb NOT NULL,deleted boolean NOT NULL DEFAULT false,PRIMARY KEY(tenant_id,id));
        ALTER TABLE sync_fixture ENABLE ROW LEVEL SECURITY;
        ALTER TABLE sync_fixture FORCE ROW LEVEL SECURITY;
        CREATE POLICY tenant ON sync_fixture USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid) WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
        GRANT SELECT,INSERT,UPDATE,DELETE ON sync_fixture TO aurora_app;
        """);
    foreach(var name in new[]{"0030_integration_sync.sql","0031_sync_import.sql","0036_sync_runtime.sql","0039_sync_source_metadata.sql"})
        await setup.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations",name)));
    await setup.ExecuteAsync("""
        INSERT INTO tenant VALUES(@tenant),(@other);
        INSERT INTO aurora_sync_connection(tenant_id,id,provider,source_identity,credential_reference,enabled)
        VALUES(@tenant,@nova,'nova','nova-test',@nova,true);
        INSERT INTO aurora_sync_mapping(tenant_id,connection_id,entity_type,local_id,external_id) VALUES(@tenant,@nova,'legacy','keep','native-keep');
        """,new{tenant,other,nova});
    await setup.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations","0042_sync_connections.sql")));
    await setup.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations","0043_freightops_sync_requests.sql")));
    await setup.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations","0044_freightops_runtime.sql")));
    Check(await setup.ExecuteScalarAsync<Guid>("SELECT connection_id FROM aurora_sync_mapping WHERE local_id='keep'")==nova,"migration preserves existing connection and mapping IDs");
}
builder.Options="-c role=aurora_app";
await using var dataSource=NpgsqlDataSource.Create(builder.ConnectionString);
var context=new Context(tenant);var factory=new NpgsqlConnectionFactory(dataSource,context);
await using var db=await factory.OpenConnectionAsync();
Check(await db.ExecuteScalarAsync<bool>("SELECT current_user='aurora_app' AND NOT rolbypassrls FROM pg_roles WHERE rolname=current_user"),"checks run with RLS enforced by runtime role");
var settings=new SyncConnections(factory,context,new Catalog());
var original=await settings.Read(default)??throw new Exception("Nova setting missing");
var second=await settings.Save(new("freightops",freight,false,0),default);freight=second.Id;
Check((await settings.Read(default))!.Id==nova,"legacy connection read still selects Nova with two providers");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_connection")==2,"tenant can retain both provider connections");
await db.ExecuteAsync("INSERT INTO aurora_order_source VALUES(@tenant,@nova,'{\"reporting\":{\"firebirdSource\":\"nova-test\"}}')",new{tenant,nova});
Check(await db.ExecuteScalarAsync<bool>("SELECT aurora_source_in_workspace(@tenant,@nova)",new{tenant,nova}),"second provider does not hide the existing Nova planning source");
await settings.Save(new("freightops",second.CredentialReference,true,second.Revision),default);
Check((await settings.Read(default))!.Enabled,"changing FreightOps does not disable Nova");
Check((await settings.ReadAll(default)).Length==2,"connection listing exposes both providers without credentials");
await using(var tx=await db.BeginTransactionAsync())
{
    await SyncStore.Enqueue(db,tx,tenant,"terminal","scope-check",1,"upsert","{}",default);
    Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE local_id='scope-check' AND connection_id=@freight",new{freight},tx)==0,"FreightOps never queues unsupported master or manifest entities");
    Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE local_id='scope-check' AND connection_id=@nova",new{nova},tx)==1,"Nova retains existing master-entity delivery");
    await tx.RollbackAsync();
}
await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=false WHERE id=@nova",new{nova});
Check((await db.QueryAsync<Guid>("SELECT tenant_id FROM public.aurora_enabled_sync_tenants()")).Contains(tenant),"scheduler discovers a tenant with only FreightOps enabled");
await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE id=@nova",new{nova});
try{await settings.Save(new("freightops",second.CredentialReference,false,second.Revision),default);throw new Exception("stale save accepted");}
catch(FormatException){Check(true,"connection revisions remain independent and optimistic");}
var entities=new Entities(context);var importer=new SyncImporter(factory,context,entities);var store=new SyncStore(factory,context);
var review=new SyncReview(factory,context,entities);
JsonObject Data(string value)=>new(){["value"]=value};
ExternalChange Change(string eventId,string value,bool deleted=false)=>new(eventId,"order","nova-order",eventId,Data(value),deleted);
async Task Import(ExternalChange e)=>await importer.Apply(nova,new([e],e.EventId),default);
await Import(Change("n1","initial"));
var localId=await db.ExecuteScalarAsync<string>("SELECT local_id FROM aurora_sync_mapping WHERE entity_type='order' AND connection_id=@nova",new{nova});
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@nova",new{nova})==0,"accepted import does not echo to Nova");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@freight",new{freight})==1,"accepted import atomically queues FreightOps delivery");
await Import(Change("n1","initial"));
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==1,"inbox replay does not duplicate onward work");
var outgoing=await store.Claim(freight,default)??throw new Exception("missing delivery");
await store.Complete(outgoing,new("fo-order","f1"),default);
await importer.Apply(freight,new([new("f1","order","fo-order","f1",Data("initial"))],"f1"),default);
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE state='pending'")==0,"target readback does not create an echo loop");
await importer.Apply(freight,new([new("f2","order","fo-order","f2",Data("from freight"))],"f2"),default);
var reverse=await store.Claim(nova,default)??throw new Exception("missing reverse delivery");
Check(JsonNode.Parse(reverse.Payload)!["value"]!.ToString()=="from freight","FreightOps import queues the reverse Nova delivery");
await store.Complete(reverse,new("nova-order","n2"),default);
await db.ExecuteAsync("UPDATE sync_fixture SET data='{\"value\":\"local\"}',revision=revision+1 WHERE id=@localId",new{localId});
await Import(Change("n3","remote"));
var conflict=(await review.Conflicts(default)).Single();
Check(conflict.ConnectionId==nova,"conflict readback identifies its connection");
await review.Resolve(conflict.Id,new(conflict.LocalRevision,Data("remote"),false),default);
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@nova AND state='pending'",new{nova})==0,"accepting source conflict value does not echo to its origin");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@freight AND state='pending'",new{freight})==1,"accepting source conflict value forwards to the other provider");
Check((await review.Status(default))!.Conflicts==0,"legacy Nova status still works with multiple connections");

var before=await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox");
try
{
    await importer.Apply(nova,new([new("rollback-1","order","new-order","r",Data("new")),new("rollback-2","order","fail","r",new JsonObject{["fail"]=true})],"bad"),default);
    throw new Exception("failed batch accepted");
}
catch(FormatException){Check(true,"invalid incoming batch rejected");}
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==before &&
    await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_inbox WHERE event_id LIKE 'rollback-%'")==0 &&
    await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_mapping WHERE external_id='new-order'")==0,
    "rollback removes business mappings, receipts and onward delivery together");
Check(await db.ExecuteScalarAsync<string>("SELECT checkpoint FROM aurora_sync_connection WHERE id=@nova",new{nova})=="n3","failed batch does not advance checkpoint");
await Import(Change("n4","remote",true));
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@freight AND operation='delete'",new{freight})==1,"tombstone forwards to other destination");

// Rebase can supersede an already queued revision without changing its local value.
await using(var tx=await db.BeginTransactionAsync())
{
    await SyncStore.Enqueue(db,tx,tenant,"order","rebase",42,"upsert","{}",default);
    await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='superseded' WHERE local_id='rebase'",transaction:tx);
    await SyncStore.Enqueue(db,tx,tenant,"order","rebase",42,"upsert","{}",default,freight);
    await tx.CommitAsync();
}
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE local_id='rebase' AND connection_id=@nova AND state='pending'",new{nova})==1,"same-revision correction revives superseded delivery instead of losing it");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE local_id='rebase' AND connection_id=@freight AND state='pending'",new{freight})==0,"excluded origin is not revived");
await using(var tx=await db.BeginTransactionAsync())
{
    try{await SyncStore.Enqueue(db,tx,tenant,"order","rebase",42,"upsert","{\"different\":true}",default);throw new Exception("mismatched revision accepted");}
    catch(FormatException){Check(true,"same revision cannot silently change payload");}
    await tx.RollbackAsync();
}
// Durable transport preparation survives baseline changes and process replacement.
await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='superseded' WHERE state IN ('pending','working')");
await using(var tx=await db.BeginTransactionAsync())
{
    await SyncStore.Enqueue(db,tx,tenant,"order","adapter",1,"upsert","{\"z\":1.25,\"a\":{\"lineId\":\"stable\",\"clear\":null}}",default,onlyConnectionId:freight);
    await db.ExecuteAsync("INSERT INTO aurora_sync_mapping(tenant_id,connection_id,entity_type,local_id,external_id,external_revision) VALUES(@tenant,@freight,'order','adapter','PRO-1','7')",new{tenant,freight},tx);
    await tx.CommitAsync();
}
var adapterChange=await store.Claim(freight,default)??throw new Exception("adapter claim missing");
var requestStore=new FreightOpsRequestStore(factory,context);
var prepared=await requestStore.Prepare(adapterChange,"freightops-test",default);
Check(prepared.ExternalId=="PRO-1"&&prepared.ExpectedRevision=="7","adapter freezes mapped ID and target version");
await db.ExecuteAsync("UPDATE aurora_sync_mapping SET external_revision='99' WHERE connection_id=@freight AND local_id='adapter'",new{freight});
var retried=await new FreightOpsRequestStore(factory,context).Prepare(adapterChange,"freightops-test",default);
Check(retried.ExpectedRevision=="7"&&FreightOpsProtocol.Hash(retried)==FreightOpsProtocol.Hash(prepared),"restart retry retains exact request hash after a newer import");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_freightops_request")==1,"preparation retries do not duplicate durable requests");
try{await requestStore.Prepare(adapterChange with{Payload="{\"wrong\":true}"},"freightops-test",default);throw new Exception("changed payload accepted");}
catch(InvalidOperationException){Check(true,"adapter refuses altered outbox payload");}
try{await requestStore.Prepare(adapterChange with{LeaseId=Guid.NewGuid()},"freightops-test",default);throw new Exception("stale lease accepted");}
catch(InvalidOperationException){Check(true,"adapter refuses stale worker lease");}
try{await requestStore.Prepare(adapterChange,"different-generation",default);throw new Exception("changed generation accepted");}
catch(InvalidOperationException){Check(true,"adapter refuses changed source generation");}
await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=false WHERE id=@freight",new{freight});
try{await requestStore.Prepare(adapterChange,"freightops-test",default);throw new Exception("disabled connection accepted");}
catch(InvalidOperationException){Check(true,"disabled adapter sends no prepared request");}
await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE id=@freight",new{freight});
try{await db.ExecuteAsync("UPDATE aurora_freightops_request SET request='{}'");throw new Exception("immutable request updated");}
catch(PostgresException e) when(e.SqlState=="42501"){Check(true,"runtime cannot rewrite prepared requests");}
await using(var tx=await db.BeginTransactionAsync())
{
    await SyncStore.Enqueue(db,tx,tenant,"order","adapter-create",1,"upsert","{}",default,onlyConnectionId:freight);
    await tx.CommitAsync();
}
var createChange=await store.Claim(freight,default)??throw new Exception("create claim missing");
var preparations=await Task.WhenAll(requestStore.Prepare(createChange,"freightops-test",default),requestStore.Prepare(createChange,"freightops-test",default));
Check(preparations.All(x=>x.ExternalId is null&&x.ExpectedRevision is null),"unmapped record uses explicit create preconditions");
Check(FreightOpsProtocol.Hash(preparations[0])==FreightOpsProtocol.Hash(preparations[1])&&
    await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_freightops_request WHERE change_id=@id",new{id=createChange.Id})==1,
    "concurrent preparation freezes exactly one request");
// Exercise the actual runner, durable request store and importer through loopback HTTP.
var web=WebApplication.CreateSlimBuilder();web.Logging.ClearProviders();web.WebHost.UseUrls("http://127.0.0.1:0");
await using var target=web.Build();
FreightOpsReceipt? targetReceipt=null;var targetWrites=0;var loseResponse=true;var acknowledged=0;
var feed=new FreightOpsEvent("http-event","order","HTTP-1","3",Data("incoming over HTTP"),false);
target.MapPost("/api/aurora-sync/v1/changes",async (HttpContext http)=>
{
    if(http.Request.Headers.Authorization!="Bearer local-test"){http.Response.StatusCode=401;return;}
    var request=(await http.Request.ReadFromJsonAsync<FreightOpsWrite>(FreightOpsProtocol.Json))!;
    if(targetReceipt is null){targetWrites++;targetReceipt=new(1,"freightops-test",freight,request.ChangeId,FreightOpsProtocol.Hash(request),"HTTP-1","2");}
    if(loseResponse){loseResponse=false;http.Response.StatusCode=503;return;}
    await http.Response.WriteAsJsonAsync(targetReceipt,FreightOpsProtocol.Json);
});
target.MapPost("/api/aurora-sync/v1/pull",()=>new FreightOpsBatch(1,"freightops-test",freight,[feed]));
target.MapPost("/api/aurora-sync/v1/acknowledge",async (FreightOpsAck ack)=>
{
    // Acknowledgement is observed only after the inbox/business transaction committed.
    await using var committed=await factory.OpenConnectionAsync();
    if(await committed.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_inbox WHERE event_id=@id",new{id=feed.EventId})!=1)
        throw new Exception("Acknowledged before import committed");
    acknowledged++;return new FreightOpsAckReceipt(1,"freightops-test",freight,ack.EventIds);
});
await target.StartAsync();
var address=new Uri(target.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
using(var adapter=new FreightOpsSyncProvider(freight,"freightops-test",address,requestStore,_=>Task.FromResult("local-test")))
{
    var runner=new SyncRunner(store,adapter,importer);
    await using(var tx=await db.BeginTransactionAsync())
    {
        var row=await entities.Write("order","http-local",Data("outbound over HTTP"),false,db,tx,default);
        await SyncStore.Enqueue(db,tx,tenant,"order",row.Id,row.Revision,"upsert",row.Data.ToJsonString(),default,onlyConnectionId:freight);
        await db.ExecuteAsync("INSERT INTO aurora_sync_mapping(tenant_id,connection_id,entity_type,local_id,external_id,external_revision) VALUES(@tenant,@freight,'order','http-local','HTTP-1','1')",new{tenant,freight},tx);
        await tx.CommitAsync();
    }
    Check(await runner.PushPending(freight,default)==0,"runner leaves uncertain HTTP delivery pending");
    await db.ExecuteAsync("UPDATE aurora_sync_outbox SET available_at=now() WHERE local_id='http-local'; UPDATE aurora_sync_mapping SET external_revision='99' WHERE local_id='http-local'");
    Check(await runner.PushPending(freight,default)==1&&targetWrites==1,"runner retries frozen request and completes one target write");
    Check(await db.ExecuteScalarAsync<string>("SELECT external_revision FROM aurora_sync_mapping WHERE local_id='http-local'")=="2","runner saves confirmed target revision");
    await runner.Pull(freight,null,default);
    Check(acknowledged==1,"HTTP feed is acknowledged after database commit");
    Check(await db.ExecuteScalarAsync<string>("SELECT data->>'value' FROM sync_fixture WHERE id='http-local'")=="incoming over HTTP","HTTP feed updates Aurora business state");
    Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE local_id='http-local' AND connection_id=@nova AND state='pending'",new{nova})==1,"HTTP import queues onward Nova delivery");
    await runner.Pull(freight,null,default);
    Check(acknowledged==2&&await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_inbox WHERE event_id='http-event'")==1,"feed replay repeats acknowledgement without importing twice");
    feed=new("http-failed","order","HTTP-2","1",new JsonObject{{"fail",true}},false);
    try{await runner.Pull(freight,null,default);throw new Exception("invalid feed imported");}
    catch(FormatException){Check(acknowledged==2,"failed business import never acknowledges target events");}
}
await target.StopAsync();
// The additive runtime migration retires unsupported unsent work only.
var legacyUnsent=Guid.NewGuid();var legacyPrepared=Guid.NewGuid();
await using(var owner=new NpgsqlConnection(raw))
{
    await owner.OpenAsync();
    await owner.ExecuteAsync("""
        INSERT INTO aurora_sync_outbox(tenant_id,id,connection_id,entity_type,local_id,local_revision,operation,payload)
        VALUES(@tenant,@legacyUnsent,@freight,'terminal','old-unsent',0,'upsert','{}'),(@tenant,@legacyPrepared,@freight,'terminal','old-prepared',0,'upsert','{}');
        INSERT INTO aurora_freightops_request(tenant_id,connection_id,change_id,request) VALUES(@tenant,@freight,@legacyPrepared,'{}');
        """,new{tenant,freight,legacyUnsent,legacyPrepared});
    await owner.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations","0044_freightops_runtime.sql")));
}
Check(await db.ExecuteScalarAsync<string>("SELECT state FROM aurora_sync_outbox WHERE id=@legacyUnsent",new{legacyUnsent})=="superseded","migration retires unsupported never-prepared legacy work");
Check(await db.ExecuteScalarAsync<string>("SELECT state FROM aurora_sync_outbox WHERE id=@legacyPrepared",new{legacyPrepared})=="pending","migration preserves prepared legacy requests for explicit review");
Check(await store.Claim(freight,default) is null,"worker cannot claim unsupported historical requests");
var otherFactory=new NpgsqlConnectionFactory(dataSource,new Context(other));
await using(var isolated=await otherFactory.OpenConnectionAsync())
{
    Check(await isolated.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==0,"other tenant cannot read either destination queue");
    Check(await isolated.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_freightops_request")==0,"other tenant cannot read prepared FreightOps payloads");
    Check(await new SyncStore(otherFactory,new Context(other)).Claim(freight,default) is null,"other tenant cannot claim FreightOps delivery");
    try{await new FreightOpsRequestStore(otherFactory,new Context(other)).Prepare(adapterChange,"freightops-test",default);throw new Exception("foreign tenant prepared request");}
    catch(InvalidOperationException){Check(true,"other tenant cannot prepare a known FreightOps change ID");}
}
Console.WriteLine($"{count} database foundation checks passed.");

sealed record Context(Guid TenantId):ITenantContext{public Guid? TenantIdOrNull=>TenantId;public Guid? UserIdOrNull=>null;}
sealed class Catalog:IIntegrationConnectionCatalog{public Task<string> Validate(string provider,Guid reference,CancellationToken ct)=>Task.FromResult(provider+"-test");}
sealed class Entities(ITenantContext tenant):ISyncEntityStore
{
    public async Task<SyncEntity?> Read(string kind,string id,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT id,revision,data::text AS Json,deleted FROM sync_fixture WHERE tenant_id=@TenantId AND id=@id FOR UPDATE",new{tenant.TenantId,id},tx,cancellationToken:ct));
        return row is null?null:new(row.Id,row.Revision,JsonNode.Parse(row.Json)!.AsObject(),row.Deleted);
    }
    public async Task<SyncEntity> Write(string kind,string? id,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
    {
        if(data["fail"]?.GetValue<bool>()==true)throw new FormatException("fixture failure");
        id??=Guid.NewGuid().ToString();
        var revision=await db.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO sync_fixture(tenant_id,id,data,deleted) VALUES(@TenantId,@id,CAST(@json AS jsonb),@deleted) ON CONFLICT(tenant_id,id) DO UPDATE SET revision=sync_fixture.revision+1,data=excluded.data,deleted=excluded.deleted RETURNING revision",new{tenant.TenantId,id,json=data.ToJsonString(),deleted},tx,cancellationToken:ct));
        return new(id,revision,data,deleted);
    }
    sealed record Row(string Id,long Revision,string Json,bool Deleted);
}
