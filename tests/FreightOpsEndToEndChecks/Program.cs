using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Aurora.Integrations.FreightOps;
using Aurora.Modules.Sync;
using FreightOps.Infrastructure;
using FreightOps_Core.Application;
using FreightOps_Core.Domain.Pros;
using FreightOps_Core.Domain.Pros.Integration;
using FreightOps_Core.Domain.Pros.Projections;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Projections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Npgsql;

var raw=Environment.GetEnvironmentVariable("FREIGHTOPS_SYNC_TEST_CONNECTION")??throw new Exception("Disposable local database required");
var info=new NpgsqlConnectionStringBuilder(raw);
if(info.Host is not ("localhost" or "127.0.0.1")||info.Database?.StartsWith("freightops_sync_check_")!=true)throw new Exception("Refusing nonlocal database");
await using(var db=new NpgsqlConnection(raw))
{
    await db.OpenAsync();await using var command=new NpgsqlCommand("SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')",db);
    if(Convert.ToInt32(await command.ExecuteScalarAsync())!=0)throw new Exception("Database must be empty");
}
var options=ServicesDependency.BaseOptions(new StoreOptions(),raw,"freightops",false);
options.AutoCreateSchemaObjects=JasperFx.AutoCreate.All;
options.Events.AddEventTypes(typeof(Pro).Assembly.GetTypes().Where(x=>x.Namespace=="FreightOps_Core.Domain.Pros.Events"));
options.Projections.Snapshot<Pro>(SnapshotLifecycle.Inline);AuroraSyncService.Configure(options);
options.Events.AddEventTypes(typeof(Pro).Assembly.GetTypes().Where(x=>x.Namespace=="FreightOps_Core.Domain.Manifest.Events"));
options.Projections.Snapshot<FreightOps_Core.Domain.Manifest.Manifest>(SnapshotLifecycle.Inline);
options.Projections.Add(new TrackingUnitProjection(),ProjectionLifecycle.Inline);
using var store=new DocumentStore(options);await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
const string token="local-fixture-only-secret-not-a-deployment-credential";
var config=new AuroraSyncOptions{Enabled=true,ConnectionId=Guid.NewGuid(),SourceIdentity="local-lykes-generation",TokenSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),OriginTerminal="TEST",NativeWeightUnit="lb",NativeVolumeUnit="ft3"};
var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");builder.Services.AddSingleton<IDocumentStore>(store);
await using var app=builder.Build();app.UseAuroraSync(config);await app.StartAsync();
var uri=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
using var provider=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(),_=>Task.FromResult(token));
using var http=new HttpClient{BaseAddress=uri};
int checks=0;void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
async Task Fails<T>(Func<Task> action,string name)where T:Exception{try{await action();}catch(T){Check(true,name);return;}throw new Exception(name);}
Check(LykesNovaMeasurements.Pounds(100,0)==100,"Nova OPL_WEIGHT is mapped as pounds");
Check(Math.Abs(LykesNovaMeasurements.CubicFeet("12X12X12",null,"in")-1)<0.000001m,"Nova inch dimensions are converted to cubic feet");
Check(Math.Abs(LykesNovaMeasurements.CubicFeet("C30.48X30.48X30.48",null,null)-1)<0.000001m,"Nova centimeter prefix is honored");
await Fails<FormatException>(()=>Task.FromResult(LykesNovaMeasurements.CubicFeet("12x12",null,"in")),"Nova area is not silently treated as volume");
await Fails<FormatException>(()=>Task.FromResult(LykesNovaMeasurements.CubicFeet("20",null,null)),"scalar cube requires an explicit unit");
Check(LykesNovaMeasurements.HandlingUnits(12,3,"pallets")==3,"confirmed pallet rule does not create twelve barcodes");
var lineId=Guid.NewGuid();var chargeId=Guid.NewGuid();
JsonObject Data(string number="LYKES-TEST")=>new()
{
    ["Id"]=number,["Customer"]="Test consignee",["Shipper"]="Test shipper",["TerminalCode"]="TEST",["Notes"]="Initial",
    ["Pieces"]=12,["Reference"]="source-only reference",["Lykes"]=new JsonObject
    {
        ["Version"]=1,["WeightUnit"]="kg",["VolumeUnit"]="m3",["Lines"]=new JsonArray
        {
            new JsonObject{["LineId"]=lineId.ToString(),["Kind"]="freight",["Sku"]="SKU-1",["HandlingUnits"]=3,["PackType"]="PAL",["TotalWeight"]=30,["TotalCube"]=2,["PO"]="PO-1",["BOL"]="BOL-1",["Notes"]="line note"},
            new JsonObject{["LineId"]=chargeId.ToString(),["Kind"]="charge",["Amount"]=125,["Keyword"]="FUEL"}
        }
    }
};
SyncChange Change(JsonObject data)=>new(Guid.NewGuid(),config.ConnectionId,"order",data["Id"]!.ToString(),1,"upsert",data.ToJsonString(),1,Guid.NewGuid());
var original=Change(Data());var receipt=await provider.Push(original,default);var repeated=await provider.Push(original,default);
Check(receipt==repeated,"real target returns original receipt after response-loss retry");
await Fails<SyncConflictException>(()=>provider.Push(original with{Payload=Data("DIFFERENT").ToJsonString()},default),"same change ID cannot be reused for a different request");
string[] barcodes;
await using(var query=store.QuerySession())
{
    var pro=(await query.LoadAsync<Pro>(receipt.ExternalId))!;barcodes=pro.LineItems.Single().TrackingUnits.Select(x=>x.Barcode).ToArray();
    Check(pro.LineItems.Count==1&&barcodes.Length==3,"twelve commercial pieces and one charge create only three handling barcodes");
    Check(Math.Abs(pro.Weight-30*2.2046226218487757)<0.0001&&Math.Abs(pro.Cubes-2*35.31466672148859)<0.0001,"explicit kg/m3 totals convert to native per-unit lb/ft3");
    Check(pro.LineItems.Single().TrackingUnits.All(x=>x.PackType=="PAL"),"pack type reaches real tracking units");
    Check(await query.Query<TrackingUnitLookup>().CountAsync()==3,"native tracking lookup projection sees every barcode");
    Check(await query.Query<SyncTargetReceipt>().CountAsync()==1,"retry creates one persisted receipt");
}
var batch=await provider.Pull(null,default);var snapshot=batch.Changes.Single();
Check(snapshot.Data["Lykes"]!["Lines"]!.AsArray().Count==2&&snapshot.Data["Reference"]!.ToString()=="source-only reference","reverse mapping retains charges and source-only fields");
Check(snapshot.Data["Pieces"]!.GetValue<int>()==12,"commercial pieces remain separate from barcode count");
var replay=await provider.Pull(null,default);Check(replay.Checkpoint==batch.Checkpoint&&JsonNode.DeepEquals(replay.Changes[0].Data,snapshot.Data),"unacknowledged snapshot is immutable");
await provider.Acknowledge(batch,default);await provider.Acknowledge(batch,default);
Check((await provider.Pull(null,default)).Changes.Count==0,"exact acknowledgement is idempotent");
// The store supplies the native baseline for the test updates, as the production Aurora ledger does.
var updated=(JsonObject)snapshot.Data.DeepClone();updated["Notes"]="";
using var updateProvider=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(receipt.ExternalId,receipt.Revision),_=>Task.FromResult(token));
var updatedReceipt=await updateProvider.Push(Change(updated),default);
await using(var query=store.QuerySession())
{
    var pro=(await query.LoadAsync<Pro>(receipt.ExternalId))!;
    Check(pro.Comment==""&&barcodes.SequenceEqual(pro.LineItems.Single().TrackingUnits.Select(x=>x.Barcode)),"explicit clear preserves physical barcodes");
}
await Fails<SyncConflictException>(()=>updateProvider.Push(Change(updated),default),"stale target revision rejected");
using(var partialProvider=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(receipt.ExternalId,updatedReceipt.Revision),_=>Task.FromResult(token)))
{
    var partial=(JsonObject)updated.DeepClone();partial.Remove("Customer");
    await Fails<FormatException>(()=>partialProvider.Push(Change(partial),default),"partial snapshot cannot silently erase mapped fields");
}
using(var grow=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(receipt.ExternalId,updatedReceipt.Revision),_=>Task.FromResult(token)))
{
    updated["Lykes"]!["Lines"]![0]!["HandlingUnits"]=4;
    updatedReceipt=await grow.Push(Change(updated),default);
    await using var query=store.QuerySession();var pro=(await query.LoadAsync<Pro>(receipt.ExternalId))!;
    Check(pro.LineItems.Single().TrackingUnits.Count==4&&pro.LineItems.Single().TrackingUnits.All(x=>x.PackType=="PAL")&&
        barcodes.SequenceEqual(pro.LineItems.Single().TrackingUnits.Take(3).Select(x=>x.Barcode)),"quantity growth preserves existing barcodes and assigns pack type to the new unit");
}
var duplicate=Data("DUPLICATE");duplicate["Lykes"]!["Lines"]!.AsArray().Add(duplicate["Lykes"]!["Lines"]![0]!.DeepClone());
await Fails<FormatException>(()=>provider.Push(Change(duplicate),default),"duplicate identities rejected");
var repeatedSku=Data("REPEAT-SKU");var extra=repeatedSku["Lykes"]!["Lines"]![0]!.DeepClone();extra["LineId"]=Guid.NewGuid().ToString();repeatedSku["Lykes"]!["Lines"]!.AsArray().Add(extra);
await Fails<FormatException>(()=>provider.Push(Change(repeatedSku),default),"repeated SKUs held for review instead of collapsed");
await using(var query=store.QuerySession())Check(await query.Events.FetchStreamStateAsync("DUPLICATE")==null&&await query.Events.FetchStreamStateAsync("REPEAT-SKU")==null,"invalid mapping leaves no partial PRO or receipt");
// A failure at the receipt INSERT must roll back domain events and enrollment too.
await using(var db=new NpgsqlConnection(raw))
{
    await db.OpenAsync();await using var command=new NpgsqlCommand("ALTER TABLE freightops.mt_doc_synctargetreceipt ADD CONSTRAINT test_receipt_atomicity CHECK(data->>'ProNumber'<>'ROLLBACK')",db);await command.ExecuteNonQueryAsync();
}
await Fails<HttpRequestException>(()=>provider.Push(Change(Data("ROLLBACK")),default),"receipt persistence failure is not reported as success");
await using(var query=store.QuerySession())Check(await query.Events.FetchStreamStateAsync("ROLLBACK")==null&&await query.Query<SyncLink>().CountAsync(x=>x.ProNumber=="ROLLBACK")==0,"receipt failure rolls back events and enrollment together");
var racing=Change(Data("RACING"));
async Task<ExternalReceipt?> Race(){try{return await provider.Push(racing,default);}catch(HttpRequestException){return null;}catch(SyncConflictException){return null;}}
var racers=await Task.WhenAll(Race(),Race());var raced=await provider.Push(racing,default);
await using(var query=store.QuerySession())Check(await query.Query<SyncTargetReceipt>().CountAsync(x=>x.ProNumber=="RACING")==1&&
    (await query.LoadAsync<Pro>("RACING"))!.LineItems.Single().TrackingUnits.Count==3,"simultaneous delivery produces one PRO, receipt and barcode set");
var voidData=Data("VOIDME");var createdToVoid=await provider.Push(Change(voidData),default);
using(var voidProvider=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(createdToVoid.ExternalId,createdToVoid.Revision),_=>Task.FromResult(token)))
{
    var deletion=Change(voidData) with{Operation="delete"};
    Check(await voidProvider.Push(deletion,default)==await voidProvider.Push(deletion,default),"cancellation retries return one receipt");
}
var beforeScan=await provider.Pull(null,default);
Check(beforeScan.Changes.Any(x=>x.ExternalId=="VOIDME"&&x.Deleted),"unhandled cancellation exports a tombstone without deleting history");
await provider.Acknowledge(beforeScan,default);
// Actual legacy domain write becomes visible without a new writer-side capture hook.
await using(var session=store.LightweightSession())
{
    var stream=await session.Events.FetchForWriting<Pro>(receipt.ExternalId,long.Parse(updatedReceipt.Revision));
    stream.Aggregate!.ClearUncommittedEvents();stream.Aggregate.LoadItem(barcodes[0],"TRAILER");stream.AppendMany(stream.Aggregate.GetUncommittedEvents());await session.SaveChangesAsync();
}
var native=await provider.Pull(null,default);Check(native.Changes.Any(x=>x.ExternalId==receipt.ExternalId),"native event-store write is discovered without a global watermark");
using var latest=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(receipt.ExternalId,native.Changes.Single(x=>x.ExternalId==receipt.ExternalId).Revision),_=>Task.FromResult(token));
await Fails<SyncConflictException>(()=>latest.Push(Change(updated),default),"scanned freight cannot be overwritten through sync");
var unauth=await http.PostAsJsonAsync("/api/aurora-sync/v1/pull",new FreightOpsPull(1,config.SourceIdentity,config.ConnectionId,200));Check(unauth.StatusCode==HttpStatusCode.Unauthorized,"machine endpoint rejects missing credential");
using var wrong=new FreightOpsSyncProvider(Guid.NewGuid(),config.SourceIdentity,uri,new Requests(),_=>Task.FromResult(token));
await Fails<SyncConflictException>(()=>wrong.Pull(null,default),"valid token cannot select another connection");
// Enrollment is explicit; it must not create a duplicate PRO or replace native freight.
long enrollVersion;
await using(var session=store.LightweightSession())
{
    var pro=Pro.Factory("EXISTING",FreightOps_Core.Domain.Pros.Enums.BillType.Standard,FreightOps_Core.Domain.Pros.Enums.RouteVia.DIRECT);
    LykesOrderMapping.Apply(pro,Data("EXISTING"),null,config);
    session.Events.StartStream<Pro>(pro.Id,pro.GetUncommittedEvents().ToArray());await session.SaveChangesAsync();
    enrollVersion=(await session.Events.FetchStreamStateAsync(pro.Id))!.Version;
}
var enrollmentData=Data("EXISTING");enrollmentData["Lykes"]!["Lines"]=new JsonArray();
var enrollment=new SyncWrite(1,config.SourceIdentity,config.ConnectionId,Guid.NewGuid(),"order","EXISTING",1,"enroll","EXISTING",enrollVersion.ToString(),enrollmentData);
async Task<SyncReceipt> Enroll()
{
    var bytes=JsonSerializer.SerializeToUtf8Bytes(enrollment,AuroraSyncEndpoints.Json);
    using var request=new HttpRequestMessage(HttpMethod.Post,"/api/aurora-sync/v1/enroll"){Content=new ByteArrayContent(bytes)};
    request.Headers.Authorization=new("Bearer",token);request.Headers.Add("X-Aurora-Request-Hash",Convert.ToHexString(SHA256.HashData(bytes)));
    using var response=await http.SendAsync(request);response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<SyncReceipt>())!;
}
var enrolled=await Enroll();Check(enrolled==await Enroll(),"existing PRO enrollment is retry-safe");
var adapterEnrollment=new FreightOpsWrite(enrollment.ProtocolVersion,enrollment.SourceIdentity,enrollment.ConnectionId,enrollment.ChangeId,
    enrollment.EntityType,enrollment.LocalId,enrollment.LocalRevision,enrollment.Operation,enrollment.ExternalId,enrollment.ExpectedRevision,enrollment.Data);
var enrollmentReceipt=await provider.Enroll(adapterEnrollment,default);
Check(enrollmentReceipt.ExternalId==enrolled.ExternalId&&enrollmentReceipt.Revision==enrolled.Revision,"administrative adapter enrollment verifies the durable target receipt");
await Fails<SyncConflictException>(()=>provider.Enroll(adapterEnrollment with{ExpectedRevision="999"},default),"enrollment cannot reuse a change ID for another expected version");
await using(var query=store.QuerySession())Check((await query.LoadAsync<Pro>("EXISTING"))!.LineItems.Single().TrackingUnits.Count==3,"enrollment preserves existing physical freight");
Check((await provider.Pull(null,default)).Changes.Any(x=>x.ExternalId=="EXISTING"),"explicitly enrolled native PRO appears in the feed");
// Confirmed Lykes v2: multiple terminals, one barcode per piece, cubic feet per piece.
config.TerminalMappings=new(){["TEST"]="TEST",["AUS"]="FO-AUS",["DFW"]="FO-DFW"};config.Validate();
await provider.Push(Change(Data("V1-AFTER-MAPPING")),default);
await using(var query=store.QuerySession())Check((await query.LoadAsync<Pro>("V1-AFTER-MAPPING"))!.LineItems.Single().TrackingUnits.Count==3,"legacy v1 count semantics remain unchanged with multiple terminals configured");
Check(config.NativeTerminal("AUS")=="FO-AUS"&&config.SharedTerminal("FO-DFW")=="DFW","terminal crosswalk is reversible and supports different native codes");
Check(LykesNovaMeasurements.Pieces(12)==12&&LykesNovaMeasurements.CubePerPiece(30,12)==2.5m,"Nova total cube converts to cubic feet per confirmed piece count");
await Fails<FormatException>(()=>Task.FromResult(LykesNovaMeasurements.Pieces(2.5m)),"fractional pieces cannot generate fractional barcodes");
JsonObject V2(string id,string terminal="AUS")
{
    var data=Data(id);data["TerminalCode"]=terminal;data["Pieces"]=3;
    var profile=data["Lykes"]!;profile["Version"]=2;profile["WeightUnit"]="lb";profile["VolumeUnit"]="ft3";
    var row=profile["Lines"]![0]!.AsObject();row.Remove("HandlingUnits");row.Remove("TotalCube");
    row["Pieces"]=3;row["CubePerPiece"]=2.5;row["PackType"]="C";row["TotalWeight"]=120;
    return data;
}
var ausReceipt=await provider.Push(Change(V2("V2-AUS")),default);
var dfwReceipt=await provider.Push(Change(V2("V2-DFW","DFW")),default);
await using(var query=store.QuerySession())
{
    var aus=(await query.LoadAsync<Pro>("V2-AUS"))!;var dfw=(await query.LoadAsync<Pro>("V2-DFW"))!;
    Check(aus.OriginTerminal=="FO-AUS"&&dfw.OriginTerminal=="FO-DFW","each shipment uses its mapped terminal, not a tenant-wide default");
    Check(aus.LineItems.Single().TrackingUnits.Count==3&&aus.LineItems.Single().Cubes==2.5&&aus.Cubes==7.5,"three pieces create three barcodes and 7.5 total cubic feet");
}
var v2Batch=await provider.Pull(null,default);var ausSnapshot=v2Batch.Changes.Single(x=>x.ExternalId=="V2-AUS");
Check(ausSnapshot.Data["TerminalCode"]!.ToString()=="AUS"&&ausSnapshot.Data["Pieces"]!.GetValue<int>()==3&&
    ausSnapshot.Data["Lykes"]!["Lines"]![0]!["CubePerPiece"]!.GetValue<double>()==2.5,"reverse projection preserves shared terminal, piece count and per-piece cube");
await provider.Acknowledge(v2Batch,default);
var edited=(JsonObject)ausSnapshot.Data.DeepClone();edited["TerminalCode"]="DFW";edited["Pieces"]=4;edited["Cube"]=10;
edited["Lykes"]!["Lines"]![0]!["Pieces"]=4;
using(var edits=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests(ausReceipt.ExternalId,ausReceipt.Revision),_=>Task.FromResult(token)))
{
    var reinterpreted=(JsonObject)ausSnapshot.Data.DeepClone();reinterpreted["Lykes"]!["Version"]=1;
    await Fails<SyncConflictException>(()=>edits.Push(Change(reinterpreted),default),"existing shipment cannot silently switch quantity interpretation between profile versions");
    await edits.Push(Change(edited),default);
    await using var query=store.QuerySession();var pro=(await query.LoadAsync<Pro>("V2-AUS"))!;
    Check(pro.OriginTerminal=="FO-DFW"&&pro.LineItems.Single().TrackingUnits.Count==4&&pro.Cubes==10,"editable shipment can change mapped terminal and add a piece without changing cube per piece");
}
foreach(var scenario in new[]{"unknown-terminal","pallet-count","wrong-total","wrong-cube","metric-v2"})
{
    var invalid=V2("INVALID-"+scenario);
    if(scenario=="unknown-terminal")invalid["TerminalCode"]="UNKNOWN";
    if(scenario=="pallet-count")invalid["Lykes"]!["Lines"]![0]!["HandlingUnits"]=1;
    if(scenario=="wrong-total")invalid["Pieces"]=12;
    if(scenario=="wrong-cube")invalid["Lykes"]!["Lines"]![0]!["TotalCube"]=100;
    if(scenario=="metric-v2")invalid["Lykes"]!["VolumeUnit"]="m3";
    await Fails<FormatException>(()=>provider.Push(Change(invalid),default),"v2 rejects "+scenario);
}
await using(var query=store.QuerySession())Check(await query.Query<SyncLink>().CountAsync(x=>x.ProNumber.StartsWith("INVALID-"))==0,"invalid v2 requests leave no enrollment or partial shipment");
// Enrollment must validate the native side of the crosswalk and return the shared side.
await using(var session=store.LightweightSession())
{
    var pro=Pro.Factory("NATIVE-AUS",FreightOps_Core.Domain.Pros.Enums.BillType.Standard,FreightOps_Core.Domain.Pros.Enums.RouteVia.DIRECT);
    LykesOrderMapping.Apply(pro,V2("NATIVE-AUS"),null,config);session.Events.StartStream<Pro>(pro.Id,pro.GetUncommittedEvents().ToArray());await session.SaveChangesAsync();
    enrollVersion=(await session.Events.FetchStreamStateAsync(pro.Id))!.Version;
}
enrollmentData=V2("NATIVE-AUS");enrollmentData["Lykes"]!["Lines"]=new JsonArray();
enrollment=new(1,config.SourceIdentity,config.ConnectionId,Guid.NewGuid(),"order","NATIVE-AUS",1,"enroll","NATIVE-AUS",enrollVersion.ToString(),enrollmentData);
await Enroll();Check((await provider.Pull(null,default)).Changes.Single(x=>x.ExternalId=="NATIVE-AUS").Data["TerminalCode"]!.ToString()=="AUS","v2 enrollment accepts a mapped native terminal and exports shared code");
config.TerminalMappings["OTHER"]="FO-AUS";
await Fails<InvalidOperationException>(()=>{config.Validate();return Task.CompletedTask;},"ambiguous reverse terminal mapping rejected at configuration validation");
config.TerminalMappings.Remove("OTHER");
var novaOptions=new Aurora.Integrations.Nova.Sync.LykesNovaOptions{Enabled=true,
    TerminalMappings=new(){{"N-AUS","AUS"}},KeywordKinds=new(){{"FRT","freight"}},
    PackTypes=new(){{"CTN","C"}},CubeBasis="per-piece",ScalarCubeUnit="ft3",DimensionUnit="in",FreightStopKind="P"};
var novaSource=Data("NOVA-BRIDGE");novaSource.Remove("Lykes");novaSource["TerminalCode"]="N-AUS";
novaSource["Status"]="Ready";novaSource["WorkType"]="Delivery";
var novaLineId=Guid.NewGuid();
var novaData=Aurora.Integrations.Nova.Sync.LykesNovaMapping.Project(novaSource,
    [new(){LineId=novaLineId.ToString(),Sequence=1,StopId=1,StopKind="P",Keyword="FRT",Sku="NOVA-SKU",Uom="CTN",Cube="2.5",Pieces=3,Weight=120,Pallets=1}],novaOptions);
await provider.Push(Change(novaData),default);
await using(var query=store.QuerySession())
{
    var pro=(await query.LoadAsync<Pro>("NOVA-BRIDGE"))!;var line=pro.LineItems.Single();
    Check(pro.OriginTerminal=="FO-AUS"&&line.LineId==novaLineId&&line.TrackingUnits.Count==3&&pro.Cubes==7.5,
        "Nova projection crosses real HTTP endpoint preserving identity, terminal, pieces and per-piece cube");
}
var novaReturn=(await provider.Pull(null,default)).Changes.Single(x=>x.ExternalId=="NOVA-BRIDGE");
Check(novaReturn.Data["Lykes"]!["Lines"]![0]!["Nova"]!["Pallets"]!.GetValue<int>()==1,
    "FreightOps reverse snapshot retains Nova-only metadata without turning pallets into barcodes");
var novaCorrection=(JsonObject)novaReturn.Data.DeepClone();novaCorrection["Pieces"]=4;novaCorrection["Weight"]=160;novaCorrection["Cube"]=14;
novaCorrection["Lykes"]!["Lines"]![0]!["Pieces"]=4;novaCorrection["Lykes"]!["Lines"]![0]!["TotalWeight"]=160;novaCorrection["Lykes"]!["Lines"]![0]!["CubePerPiece"]=3.5;
await provider.Acknowledge(await provider.Pull(null,default),default);
using(var correctionProvider=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests("NOVA-BRIDGE",novaReturn.Revision),_=>Task.FromResult(token)))
    await correctionProvider.Push(Change(novaCorrection),default);
var novaEditedReturn=(await provider.Pull(null,default)).Changes.Single(x=>x.ExternalId=="NOVA-BRIDGE");
var lifecycleId=Guid.NewGuid();var lifecycleRevision=novaEditedReturn.Revision;
async Task<JsonObject> FreightLifecycle(JsonObject normalized,bool adding)
{
    await provider.Acknowledge(await provider.Pull(null,default),default);
    // Model the normalization import returning the new Nova metadata to FreightOps.
    using(var normalizer=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests("NOVA-BRIDGE",lifecycleRevision),_=>Task.FromResult(token)))
        lifecycleRevision=(await normalizer.Push(Change(normalized),default)).Revision;
    var desired=(JsonObject)normalized.DeepClone();var rows=desired["Lykes"]!["Lines"]!.AsArray();
    if(adding)rows.Add(new JsonObject{["LineId"]=lifecycleId.ToString(),["Kind"]="freight",["Sku"]="LIFECYCLE-SKU",["PackType"]="C",["Pieces"]=2,["TotalWeight"]=20,["CubePerPiece"]=1.5});
    else rows.Remove(rows.OfType<JsonObject>().Single(x=>x["LineId"]!.GetValue<string>()==lifecycleId.ToString()));
    desired["Pieces"]=adding?6:4;desired["Weight"]=adding?180:160;desired["Cube"]=adding?17:14;
    using(var editor=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests("NOVA-BRIDGE",lifecycleRevision),_=>Task.FromResult(token)))
        lifecycleRevision=(await editor.Push(Change(desired),default)).Revision;
    return (await provider.Pull(null,default)).Changes.Single(x=>x.ExternalId=="NOVA-BRIDGE").Data;
}
var newOrder=V2("900008");newOrder["Status"]="Ready";newOrder["WorkType"]="Delivery";
newOrder["ScheduledAt"]="2026-10-28T10:00:00-04:00";
newOrder["Lykes"]!["Lines"]=new JsonArray(newOrder["Lykes"]!["Lines"]![0]!.DeepClone());
newOrder["Lykes"]!["Lines"]![0]!["LineId"]=Guid.NewGuid().ToString();
var newOrderReceipt=await provider.Push(Change(newOrder),default);
var newOrderSnapshot=(await provider.Pull(null,default)).Changes.Single(x=>x.ExternalId=="900008");
async Task<JsonObject> CancelNewOrder(JsonObject normalized)
{
    await provider.Acknowledge(await provider.Pull(null,default),default);
    ExternalReceipt bound;
    using(var binding=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests("900008",newOrderReceipt.Revision),_=>Task.FromResult(token)))
        bound=await binding.Push(Change(normalized),default);
    var changedId=(JsonObject)normalized.DeepClone();changedId["NativeOrderId"]=999999;
    using(var locked=new FreightOpsSyncProvider(config.ConnectionId,config.SourceIdentity,uri,new Requests("900008",bound.Revision),_=>Task.FromResult(token)))
    {
        await Fails<FormatException>(()=>locked.Push(Change(changedId),default),"bound Nova order ID cannot be reassigned in FreightOps");
        await locked.Push(Change(normalized) with{Operation="delete"},default);
    }
    var cancelled=(await provider.Pull(null,default)).Changes.Single(x=>x.ExternalId=="900008");
    Check(cancelled.Deleted,"FreightOps emits a cancellation tombstone for the new order");
    return cancelled.Data;
}
await NovaReverseBridge.Run(novaData,novaEditedReturn.Data,novaOptions,Check,FreightLifecycle,newOrderSnapshot.Data,CancelNewOrder,
    (db,cs)=>ProductionMappingChecks.Run(db,cs,raw,uri,config,token,Check,store));
config.Enabled=false;var disabled=await http.PostAsJsonAsync("/api/aurora-sync/v1/pull",new{});Check(disabled.StatusCode==HttpStatusCode.NotFound,"disabled endpoint has no active contract");
await app.StopAsync();Console.WriteLine($"{checks} real FreightOps endpoint checks passed.");

sealed class Requests(string? external=null,string? version=null):IFreightOpsRequestStore
{
    public Task<FreightOpsWrite> Prepare(SyncChange c,string source,CancellationToken ct)=>Task.FromResult(new FreightOpsWrite(1,source,c.ConnectionId,c.Id,c.EntityType,c.LocalId,c.LocalRevision,c.Operation,external,version,JsonNode.Parse(c.Payload)!.AsObject()));
}
