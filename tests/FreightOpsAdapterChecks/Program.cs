using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Integrations.FreightOps;
using Aurora.Modules.Sync;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

// Real loopback HTTP contract server. It is a test double for the future FreightOps
// v1 endpoints, not proof of domain mapping or production target transaction behavior.
var builder=WebApplication.CreateSlimBuilder(args);
builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app=builder.Build();
Func<HttpContext,Task> handler=_=>throw new Exception("Unexpected request");
var requestCount=0;var bearer="local-test-token";
app.Run(async context=>
{
    requestCount++;
    if(context.Request.Headers.Authorization!="Bearer "+bearer){context.Response.StatusCode=401;return;}
    await handler(context);
});
await app.StartAsync();
var uri=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
var connection=Guid.NewGuid();const string source="freightops-test-generation-1";
var store=new MemoryRequests();
using var provider=new FreightOpsSyncProvider(connection,source,uri,store,_=>Task.FromResult(bearer));
var change=new SyncChange(Guid.NewGuid(),connection,"order","local-1",1,"upsert","{\"lines\":[{\"lineId\":\"stable-1\",\"quantity\":2}],\"notes\":null}",1,Guid.NewGuid());
int count=0;
void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
async Task Fails<T>(Func<Task> action,string name) where T:Exception
{
    try{await action();}catch(T){Check(true,name);return;}throw new Exception(name+" did not fail");
}
Task Json(HttpContext c,object value)=>c.Response.WriteAsJsonAsync(value,FreightOpsProtocol.Json);
FreightOpsWrite? written=null;FreightOpsReceipt? saved=null;int writes=0;bool loseResponse=true;
handler=async c=>
{
    Check(c.Request.Path=="/api/aurora-sync/v1/changes","push uses isolated versioned endpoint");
    var input=await c.Request.ReadFromJsonAsync<FreightOpsWrite>(FreightOpsProtocol.Json)??throw new Exception();
    written=input;
    Check(c.Request.Headers["X-Aurora-Request-Hash"]==FreightOpsProtocol.Hash(input),"request hash accompanies the immutable wire payload");
    if(saved is null){writes++;saved=new(1,source,connection,input.ChangeId,FreightOpsProtocol.Hash(input),"PRO-123","7");}
    if(loseResponse){loseResponse=false;c.Response.StatusCode=503;return;}
    await Json(c,saved);
};
await Fails<HttpRequestException>(()=>provider.Push(change,default),"uncertain response stays retryable");
var receipt=await provider.Push(change with{Attempts=2,LeaseId=Guid.NewGuid()},default);
Check(writes==1&&receipt.ExternalId=="PRO-123","response-loss retry uses same change and receipt");
Check(written!.Data["notes"] is null&&written.Data.ContainsKey("notes")&&written.Data["lines"]![0]!["lineId"]!.ToString()=="stable-1","transport retains explicit clear and stable line identity");
var previous=requestCount;
await Fails<InvalidOperationException>(()=>provider.Push(change with{ConnectionId=Guid.NewGuid()},default),"wrong connection rejected before HTTP");
Check(previous==requestCount,"wrong connection sends no credentials");
foreach(var status in new[]{409,412})
{
    handler=c=>{c.Response.StatusCode=status;return Task.CompletedTask;};
    await Fails<SyncConflictException>(()=>provider.Push(change,default),$"HTTP {status} requires conflict review");
}
foreach(var status in new[]{400,422})
{
    handler=c=>{c.Response.StatusCode=status;return Task.CompletedTask;};
    await Fails<FormatException>(()=>provider.Push(change,default),$"HTTP {status} rejects unsupported data");
}
foreach(var status in new[]{202,204,401,403,429,500,503})
{
    handler=c=>{c.Response.StatusCode=status;return status==204?Task.CompletedTask:c.Response.WriteAsync("sensitive remote details");};
    try{await provider.Push(change,default);throw new Exception("expected error");}
    catch(HttpRequestException e){Check(!e.Message.Contains("sensitive"),$"HTTP {status} retries without persisting remote details");}
}
handler=c=>{c.Response.StatusCode=307;c.Response.Headers.Location=uri+"redirect-target";return Task.CompletedTask;};
previous=requestCount;await Fails<HttpRequestException>(()=>provider.Push(change,default),"redirect rejected");
Check(requestCount==previous+1,"redirect never forwards bearer credentials");
handler=c=>Json(c,saved! with{RequestHash="wrong"});
await Fails<FormatException>(()=>provider.Push(change,default),"wrong request receipt rejected");
handler=c=>Json(c,saved! with{SourceIdentity="restored-database"});
await Fails<FormatException>(()=>provider.Push(change,default),"source generation change rejected");
handler=c=>Json(c,saved! with{ConnectionId=Guid.NewGuid()});
await Fails<FormatException>(()=>provider.Push(change,default),"wrong receipt connection rejected");
handler=c=>Json(c,saved! with{ChangeId=Guid.NewGuid()});
await Fails<FormatException>(()=>provider.Push(change,default),"wrong receipt change rejected");
handler=c=>{c.Response.ContentType="application/json";return c.Response.WriteAsync("{not-json");};
await Fails<FormatException>(()=>provider.Push(change,default),"malformed successful response rejected");
handler=c=>{c.Response.ContentType="text/html";return c.Response.WriteAsync("<html>login</html>");};
await Fails<FormatException>(()=>provider.Push(change,default),"HTML login response cannot complete delivery");
handler=c=>{c.Response.ContentType="application/json";c.Response.ContentLength=5*1024*1024;return c.Response.WriteAsync(" ");};
await Fails<FormatException>(()=>provider.Push(change,default),"oversize response rejected");
var events=new[]{new FreightOpsEvent("event-21","order","PRO-123","8",new JsonObject{{"notes","changed"}},false),
    new FreightOpsEvent("event-17","order","PRO-456","4",new JsonObject(),true)};
handler=async c=>
{
    Check(c.Request.Path=="/api/aurora-sync/v1/pull","pull uses versioned endpoint");
    var input=await c.Request.ReadFromJsonAsync<FreightOpsPull>(FreightOpsProtocol.Json);
    Check(input!.ConnectionId==connection&&input.SourceIdentity==source&&input.Limit==200,"bounded pull pins connection and source");
    await Json(c,new FreightOpsBatch(1,source,connection,events));
};
var batch=await provider.Pull(null,default);
Check(batch.Changes.Count==2&&batch.Changes[1].Deleted,"pull preserves updates and tombstones without sequence assumptions");
int acks=0;
handler=async c=>
{
    Check(c.Request.Path=="/api/aurora-sync/v1/acknowledge","ack uses isolated endpoint");
    var ack=(await c.Request.ReadFromJsonAsync<FreightOpsAck>(FreightOpsProtocol.Json))!;
    Check(ack.EventIds.SequenceEqual(new[]{"event-21","event-17"}),"ack contains exact events including late lower sequence");
    acks++;await Json(c,new FreightOpsAckReceipt(1,source,connection,ack.EventIds));
};
await provider.Acknowledge(batch,default);await provider.Acknowledge(batch,default);
Check(acks==2,"acknowledgement can be retried independently");
previous=requestCount;
await Fails<FormatException>(()=>provider.Acknowledge(batch with{Changes=[batch.Changes[0]]},default),"partial import cannot acknowledge the full batch");
await Fails<FormatException>(()=>provider.Pull("123456",default),"numeric watermark checkpoint rejected");
Check(previous==requestCount,"invalid acknowledgement and checkpoint rejected before HTTP");
handler=c=>Json(c,new FreightOpsBatch(1,source,connection,[events[0],events[0]]));
await Fails<FormatException>(()=>provider.Pull(null,default),"duplicate event IDs rejected before import");
handler=c=>Json(c,new FreightOpsBatch(2,source,connection,events));
await Fails<FormatException>(()=>provider.Pull(null,default),"unknown protocol rejected");
handler=c=>Json(c,new FreightOpsBatch(1,source,connection,[events[0] with{EntityType="terminal"}]));
await Fails<FormatException>(()=>provider.Pull(null,default),"unsupported incoming entity is rejected before import");
var manifestEvent=new FreightOpsEvent("manifest-event-1","manifest","MAN-123","9",new JsonObject
{
    ["Number"]="MAN-123",["Status"]="Planning",["Vehicle"]="TRUCK-2",
    ["Details"]=new JsonObject{["Driver"]="Driver changed in FreightOps"},["Orders"]=new JsonArray()
},false);
handler=c=>Json(c,new FreightOpsBatch(1,source,connection,[manifestEvent]));
var manifestBatch=await provider.Pull(null,default);
Check(manifestBatch.Changes.Single().EntityType=="manifest" &&
    JsonNode.DeepEquals(manifestBatch.Changes.Single().Data,manifestEvent.Data),
    "reverse manifest feed retains native edits and membership");
handler=c=>Json(c,new FreightOpsBatch(1,source,connection,[]));
var empty=await provider.Pull(batch.Checkpoint,default);previous=requestCount;
await provider.Acknowledge(empty,default);Check(previous==requestCount,"empty batch needs no acknowledgement request");
using var cancelled=new CancellationTokenSource();cancelled.Cancel();
await Fails<OperationCanceledException>(()=>provider.Pull(null,cancelled.Token),"cancellation propagates");
foreach(var address in new[]{"http://example.com","https://user:secret@example.com","https://example.com/?token=secret","https://example.com/#fragment"})
{
    try{using var invalid=new FreightOpsSyncProvider(connection,source,new Uri(address),store,_=>Task.FromResult(bearer));throw new Exception("bad endpoint accepted");}
    catch(ArgumentException){Check(true,"unsafe installation URL rejected");}
}
await app.StopAsync();Console.WriteLine($"{count} FreightOps adapter HTTP checks passed.");

sealed class MemoryRequests:IFreightOpsRequestStore
{
    readonly Dictionary<Guid,FreightOpsWrite> rows=[];
    public Task<FreightOpsWrite> Prepare(SyncChange c,string source,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(!rows.TryGetValue(c.Id,out var request))rows[c.Id]=request=new(1,source,c.ConnectionId,c.Id,c.EntityType,c.LocalId,c.LocalRevision,c.Operation,null,null,JsonNode.Parse(c.Payload)!.AsObject());
        return Task.FromResult(request);
    }
}
