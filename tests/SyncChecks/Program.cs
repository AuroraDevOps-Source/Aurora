using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

int count=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
JsonObject Obj(string value)=>JsonNode.Parse(value)!.AsObject();
Check(SyncHistoryWindow.Cutoff(new(2026,10,23))==new DateOnly(2026,7,23),"sync history uses three calendar months");
Check(SyncHistoryWindow.Cutoff(new(2026,5,31))==new DateOnly(2026,2,28),"history cutoff handles month ends");
Check(SyncHistoryWindow.Includes("order",new(2026,7,23),false,false,new(2026,10,23)),"cutoff date is included");
Check(!SyncHistoryWindow.Includes("manifest",new(2026,7,22),false,false,new(2026,10,23)),"older closed history is excluded");
Check(SyncHistoryWindow.Includes("order",new(2020,1,1),true,false,new(2026,10,23)),"older open work remains in scope");
Check(SyncHistoryWindow.Includes("order",new(2020,1,1),false,true,new(2026,10,23)),"current work dependencies remain in scope");
Check(SyncHistoryWindow.Includes("driver",new(2020,1,1),false,false,new(2026,10,23)),"master records do not age out with order history");
Check(SyncJson.Equal(Obj("""{"ScheduledAt":"2026-10-26T10:00:00-04:00"}"""),Obj("""{"ScheduledAt":"2026-10-26T14:00:00+00:00"}""")),"UTC storage does not invent a local timestamp edit");
Check(!SyncJson.Equal(Obj("""{"ScheduledAt":"2026-10-26T10:00:00-04:00"}"""),Obj("""{"ScheduledAt":"2026-10-26T15:00:00+00:00"}""")),"different appointment instants remain changes");
var baseline=Obj("""{"address":"Old","phone":"1","unchanged":null}""");
var local=Obj("""{"address":"Local","phone":"1","unchanged":null}""");
var external=Obj("""{"address":"Old","phone":"2","unchanged":null}""");
var merge=SyncMerge.Apply(baseline,local,external);
Check(merge.Conflicts.Length==0&&merge.Result["address"]!.ToString()=="Local"&&merge.Result["phone"]!.ToString()=="2","independent changes merge");
external["address"]="Remote";
merge=SyncMerge.Apply(baseline,local,external);
Check(merge.Conflicts.SequenceEqual(new[]{"address"})&&merge.Result["address"]!.ToString()=="Local","competing change remains a conflict without data loss");
Check(!SyncMerge.Apply(Obj("{}"),Obj("{}"),Obj("""{"a":null}""")).Result.ContainsKey("missing"),"absent keys stay absent");
Check(SyncMerge.Apply(Obj("{}"),Obj("{}"),Obj("""{"a":null}""")).Result.ContainsKey("a"),"explicit null differs from missing");
Check(!SyncMerge.Apply(Obj("""{"a":1}"""),Obj("""{"a":1}"""),Obj("{}")).Result.ContainsKey("a"),"remote removal merges when local did not change");
Check(SyncMerge.Apply(Obj("""{"a":1}"""),Obj("""{"a":2}"""),Obj("""{"a":2}""")).Conflicts.Length==0,"matching concurrent edits are not conflicts");
if(Environment.GetEnvironmentVariable("SYNC_TEST_CONNECTION") is not {Length:>0} connection){Console.WriteLine($"{count} merge checks passed; database checks require SYNC_TEST_CONNECTION.");return;}
var builder=new NpgsqlConnectionStringBuilder(connection);
if(!builder.Database!.StartsWith("aurora_sync_check_",StringComparison.Ordinal))throw new Exception("Disposable sync test database required.");
await using var dataSource=NpgsqlDataSource.Create(connection);
var tenant=Guid.NewGuid();var other=Guid.NewGuid();
var factory=new NpgsqlConnectionFactory(dataSource,new Context(tenant));
var otherFactory=new NpgsqlConnectionFactory(dataSource,new Context(other));
var store=new SyncStore(factory,new Context(tenant));
var connectionId=Guid.NewGuid();
await using var db=await factory.OpenConnectionAsync();
await db.ExecuteAsync("INSERT INTO tenant(id) VALUES(@tenant),(@other)",new{tenant,other});
await db.ExecuteAsync("INSERT INTO aurora_sync_connection(tenant_id,id,provider,source_identity,credential_reference) VALUES(@tenant,@connectionId,'nova','test',@credential)",new{tenant,connectionId,credential=Guid.NewGuid()});
async Task Add(string id,long revision,string payload="{}"){
 await using var tx=await db.BeginTransactionAsync();
 await SyncStore.Enqueue(db,tx,tenant,"order",id,revision,"upsert",payload,default);
 await tx.CommitAsync();
}
await using(var tx=await db.BeginTransactionAsync()){
 await SyncStore.Enqueue(db,tx,tenant,"order","rollback",0,"upsert","{}",default);
 await tx.RollbackAsync();
}
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==0,"business rollback removes queued event");
await Add("A",1);await Add("A",1);
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==1,"identical enqueue retries do not duplicate");
try{await Add("A",1,"""{"changed":true}""");throw new Exception("Mismatched duplicate accepted");}catch(FormatException){Check(true,"different payload for same revision rejected");}
Check(await store.Claim(connectionId,default) is null,"disabled connection retains pending work");
await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE tenant_id=@tenant",new{tenant});
await Add("A",2);await Add("B",1);
var first=await store.Claim(connectionId,default)??throw new Exception("Missing first claim");
Check(first.LocalId=="A"&&first.LocalRevision==1,"first revision claimed first");
var second=await store.Claim(connectionId,default)??throw new Exception("Missing independent claim");
Check(second.LocalId=="B","independent record proceeds while earlier revision is leased");
Check(await store.Claim(connectionId,default) is null,"second worker cannot claim active lease or later revision");
await store.Complete(second,new("external-b","r1"),default);
await store.Retry(first,true,default);
Check(await store.Claim(connectionId,default) is null,"conflict blocks later revisions of the same record");
await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='working',lease_id=@LeaseId,lease_until=now()-interval '1 second' WHERE id=@Id",first);
var reclaimed=await store.Claim(connectionId,default)??throw new Exception("Lease was not reclaimed");
Check(reclaimed.Id==first.Id&&reclaimed.LeaseId!=first.LeaseId,"crashed worker lease reclaimed with new ownership");
try{await store.Complete(first,new("external-a","r1"),default);throw new Exception("Stale acknowledgement accepted");}catch(InvalidOperationException){Check(true,"stale worker cannot acknowledge another worker's lease");}
await store.Complete(reclaimed,new("external-a","r1"),default);
var next=await store.Claim(connectionId,default)??throw new Exception("Next revision missing");
Check(next.LocalRevision==2,"later revision starts only after earlier acknowledgement");
await store.Retry(next,false,default);
Check(await store.Claim(connectionId,default) is null,"transient failures use backoff");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_mapping")==2,"acknowledgements persist stable external mappings");
await using(var otherDb=await otherFactory.OpenConnectionAsync()){
 Check(await otherDb.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==0,"other tenant cannot read queue");
 Check(await otherDb.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_mapping")==0,"other tenant cannot read mappings");
 Check(await new SyncStore(otherFactory,new Context(other)).Claim(connectionId,default) is null,"other tenant cannot claim connection");
}
var settings=new SyncConnections(factory,new Context(tenant),new Catalog());
var before=await settings.Read(default)??throw new Exception("Missing connection settings");
var changed=await settings.Save(new("nova",before.CredentialReference,false,before.Revision),default);
Check(!changed.Enabled&&changed.Id==before.Id,"disabling a connection retains its identity and records");
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_mapping")==2,"disconnect does not delete external mappings");
try{await settings.Save(new("nova",before.CredentialReference,true,before.Revision),default);throw new Exception("Stale connection accepted");}catch(FormatException){Check(true,"stale connection configuration rejected");}
Check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_connection")==1,"one connection remains per tenant");
var nestedMerge=SyncMerge.Apply(JsonNode.Parse("{\"Unit\":{\"Alias\":\"Original\",\"Terminal\":\"101\"}}")!.AsObject(),JsonNode.Parse("{\"Unit\":{\"Alias\":\"Local\",\"Terminal\":\"101\"}}")!.AsObject(),JsonNode.Parse("{\"Unit\":{\"Alias\":\"Original\",\"Terminal\":\"102\"}}")!.AsObject());
Check(nestedMerge.Conflicts.Length==0&&nestedMerge.Result["Unit"]!["Alias"]!.GetValue<string>()=="Local"&&nestedMerge.Result["Unit"]!["Terminal"]!.GetValue<string>()=="102","nested independent equipment fields merge without conflict");
await ImportChecks.Run(factory,new Context(tenant),connectionId,Check);
await LocalDriverChecks.Run(factory,new Context(tenant),connectionId,Check);
await LocalPlanningChecks.Run(factory,new Context(tenant),Check);
await MasterSyncChecks.Run(factory,new Context(tenant),connectionId,Check);
await OrderSyncChecks.Run(factory,new Context(tenant),connectionId,Check);
await EquipmentSyncChecks.Run(factory,new Context(tenant),connectionId,Check);
await using(var otherDb=await otherFactory.OpenConnectionAsync()){
 await otherDb.ExecuteAsync("INSERT INTO aurora_terminal(tenant_id,id,code,name) VALUES(@other,gen_random_uuid(),'101','Test terminal')",new{other});
 var localOnly=new Aurora.Modules.Routing.DriverStore(otherFactory,new Context(other));
 Check((await localOnly.List(default)).Length==0,"other tenant cannot read driver profiles");
 var driver=await localOnly.Save(0,new(){Number="D1",LastName="Standalone",Terminal="101",PerHour=20},default);
 Check(driver.Id>0&&(await localOnly.List(default)).Length==1,"standalone tenant can save drivers without a Nova connection");
 Check(await otherDb.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox")==0,"standalone saves create no integration dependency or outbound work");
}
Console.WriteLine($"{count} sync checks passed.");
sealed record Context(Guid Id):ITenantContext{public Guid? TenantIdOrNull=>Id;public Guid? UserIdOrNull=>null;}

sealed class Catalog:IIntegrationConnectionCatalog{public Task<string> Validate(string provider,Guid reference,CancellationToken ct)=>Task.FromResult("test");}
