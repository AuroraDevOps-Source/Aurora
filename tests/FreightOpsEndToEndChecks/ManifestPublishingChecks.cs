using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.FreightOps;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;
using FreightOps_Core.Domain.Pros;
using FreightOps_Core.Domain.Pros.Integration;
using FreightOps_Core.Domain.Manifest.Enums;
using Marten;
using Npgsql;
using NativeManifest=FreightOps_Core.Domain.Manifest.Manifest;

static class ManifestPublishingChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,NpgsqlConnection pg,SyncStore queue,FreightOpsSyncProvider remote,AuroraSyncOptions target,IDocumentStore store,Action<bool,string> check)
    {
        await pg.ExecuteAsync("""
            CREATE TABLE aurora_manifest(tenant_id uuid,id uuid,number text,manifest_date date,status text,vehicle text,details jsonb,data jsonb,revision int,deleted_at timestamptz,draft_id uuid);
            CREATE TABLE IF NOT EXISTS aurora_planning_draft(tenant_id uuid,id uuid,source_id uuid);
            ALTER TABLE aurora_manifest_order ADD COLUMN stop_number int;
            """);
        var id=Guid.NewGuid();var connection=target.ConnectionId;
        var source=await pg.QuerySingleAsync<Guid>("SELECT source_id FROM aurora_order WHERE id='910030'");
        await pg.ExecuteAsync("""
            INSERT INTO aurora_manifest VALUES(@TenantId,@id,'MAN-LOCAL-TEST','2026-11-02','Planning','DEMO-TRUCK',CAST(@details AS jsonb),'{}',1,NULL,NULL);
            INSERT INTO aurora_manifest_order(tenant_id,source_id,order_id,manifest_id,stop_number) VALUES(@TenantId,@source,'910030',@id,1);
            """,new{tenant.TenantId,id,source,details=JsonSerializer.Serialize(new{Driver="Demo Driver",OriginTerminal="AUS",Direction="Outbound",Trailer="",Carrier="",Notes="Local plan"})});
        var publisher=new FreightOpsManifestPublishing(factory,tenant);
        async Task Rejected<T>(Func<Task> action,string name) where T:Exception
        {try{await action();}catch(T){check(true,name);return;}throw new Exception(name);}
        await Rejected<FormatException>(()=>publisher.Publish(id,0,connection,default),"stale Aurora manifest revision cannot publish");
        var nativeQueued=await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='manifest'");
        await publisher.Publish(id,1,connection,default);await publisher.Publish(id,1,connection,default);
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='manifest'")==nativeQueued+1,"repeated publish creates one FreightOps delivery and no Nova write");
        await Rejected<FormatException>(()=>new TmsStore(factory,tenant).ChangeOrders(id,new([],1),false,true,default),"submitted route cannot be deleted locally while remote delivery may exist");
        check(!(await new TmsStore(factory,tenant).Manifests(default)).Single(m=>m.Id==id).Editable,"manifest read model tells UI that a submitted route is locked");
        var change=await queue.Claim(connection,default)??throw new Exception("Manifest not claimable");
        check(change.EntityType=="manifest","production worker claims explicit manifest publish");
        await Rejected<FormatException>(()=>remote.Push(change,default),"FreightOps manifest feature is opt-in");
        target.ManifestPublishingEnabled=true;
        var receipt=await remote.Push(change,default);
        check(receipt==await remote.Push(change,default),"manifest response-loss replay returns original receipt");
        await queue.Complete(change,receipt,default);
        await using(var q=store.QuerySession())
        {
            var m=(await q.LoadAsync<NativeManifest>(receipt.ExternalId))!;var pro=(await q.LoadAsync<Pro>("910030"))!;
            check(m.Id=="MAN-LOCAL-TEST"&&m.TransitStatus==ManifestStatus.Planning,"manifest is visible as native planning, never dispatched");
            check(m.SystemUnit=="DEMO-TRUCK"&&m.Tag=="Driver: Demo Driver"&&m.LoadDate.Date==new DateTime(2026,11,2),"truck, driver reference and manifest date reach native model");
            check(m.HasPro(pro.Id)&&pro.CurrentOutboundManifest==m.Id&&m.Queue.Total==2&&m.Queue.Loaded==0,"native membership and piece queue commit together without loading freight");
            check(m.Stops.Single().Order==1&&m.Notes.Contains("Local plan"),"native delivery stop and notes retained");
        }
        var prepared=await pg.QuerySingleAsync<string>("SELECT request::text FROM aurora_freightops_request WHERE change_id=@Id",new{change.Id});
        var request=JsonSerializer.Deserialize<FreightOpsWrite>(prepared,FreightOpsProtocol.Json)!;
        check(request.Data["FreightOpsOrders"]![0]!["ExternalId"]!.ToString()=="910030","durable production request resolves native PRO through connection mapping");
        var service=new AuroraManifestPublisher(store,target);
        SyncWrite Native(FreightOpsWrite r)=>new(r.ProtocolVersion,r.SourceIdentity,r.ConnectionId,r.ChangeId,r.EntityType,r.LocalId,r.LocalRevision,r.Operation,r.ExternalId,r.ExpectedRevision,r.Data);
        var wrong=request with{ConnectionId=Guid.NewGuid(),ChangeId=Guid.NewGuid()};
        await Rejected<SyncTargetConflict>(()=>service.Change(Native(wrong),FreightOpsProtocol.Hash(wrong),default),"wrong connection cannot publish a manifest");
        var collision=request with{ChangeId=Guid.NewGuid(),LocalId=Guid.NewGuid().ToString()};
        await Rejected<SyncTargetConflict>(()=>service.Change(Native(collision),FreightOpsProtocol.Hash(collision),default),"existing native manifest number is never silently adopted");
        var secondData=(JsonObject)request.Data.DeepClone();secondData["Number"]="MAN-ATOMIC-FAIL";
        var second=request with{ChangeId=Guid.NewGuid(),LocalId=Guid.NewGuid().ToString(),Data=secondData};
        await using(var q=store.QuerySession())second.Data["FreightOpsOrders"]![0]!["ExpectedRevision"]=(await q.Events.FetchStreamStateAsync("910030"))!.Version.ToString();
        await Rejected<SyncTargetConflict>(()=>service.Change(Native(second),FreightOpsProtocol.Hash(second),default),"already assigned shipment cannot be stolen by another route");
        await using(var q=store.QuerySession())check(await q.Events.FetchStreamStateAsync("MAN-ATOMIC-FAIL")==null,"rejected assignment leaves no partial native manifest");
        await using(var q=store.QuerySession())request.Data["FreightOpsOrders"]![0]!["ExpectedRevision"]=(await q.Events.FetchStreamStateAsync("910030"))!.Version.ToString();
        request.Data["Details"]!["Driver"]="Updated Driver";
        var update=request with{ChangeId=Guid.NewGuid(),ExternalId=receipt.ExternalId,ExpectedRevision=receipt.Revision,LocalRevision=2};
        var updated=await service.Change(Native(update),FreightOpsProtocol.Hash(update),default);
        await using(var q=store.QuerySession())check((await q.LoadAsync<NativeManifest>(receipt.ExternalId))!.Tag=="Driver: Updated Driver","unstarted route metadata can be republished with correct revisions");
        // Committed native edits must make a later Aurora publish fail its expected version.
        await using(var s=store.LightweightSession())
        {
            var m=await s.Events.FetchForWriting<NativeManifest>(receipt.ExternalId);m.Aggregate!.ClearUncommittedEvents();m.Aggregate.UpdateTag("Native dispatcher edit");m.AppendMany(m.Aggregate.GetUncommittedEvents());await s.SaveChangesAsync();
        }
        var conflict=update with{ChangeId=Guid.NewGuid(),ExpectedRevision=updated.Revision};
        try{await service.Change(Native(conflict),FreightOpsProtocol.Hash(conflict),default);throw new Exception("Stale publish accepted");}
        catch(Exception ex) when(ex.GetType().Name.Contains("Concurrency")||ex.GetType().Name.Contains("UnexpectedMaxEventId")){check(true,"native manifest edits reject stale publish");}
        await using(var s=store.LightweightSession())
        {
            var m=await s.Events.FetchForWriting<NativeManifest>(receipt.ExternalId);m.Aggregate!.ClearUncommittedEvents();m.Aggregate.ReleaseOutbound("DOOR-1","TRAILER-1");m.AppendMany(m.Aggregate.GetUncommittedEvents());await s.SaveChangesAsync();
        }
        await using(var q=store.QuerySession())conflict=conflict with{ChangeId=Guid.NewGuid(),ExpectedRevision=(await q.Events.FetchStreamStateAsync(receipt.ExternalId))!.Version.ToString()};
        await Rejected<SyncTargetConflict>(()=>service.Change(Native(conflict),FreightOpsProtocol.Hash(conflict),default),"released native manifest rejects planning writes even with current revision");
        await using(var s=store.LightweightSession())
        {
            var manifestEvents=await s.Events.FetchStreamAsync(receipt.ExternalId);
            var proEvents=await s.Events.FetchStreamAsync("910030");
            check(manifestEvents.All(e=>e.Headers?.ContainsKey("user")==true||e.Data.GetType().Name is "TagUpdatedEvent" or "ManifestReleasedOutboundEvent"),"machine manifest events record an explicit service actor");
            var me=manifestEvents.First();var pe=proEvents.First();
            me.Headers!.Remove("user");pe.Headers!.Remove("user");
            var page=new JasperFx.Events.Projections.EventRange(new JasperFx.Events.Projections.ShardName("fixture"),0,me.Sequence,null!){Events=[me]};
            await new FreightOps_Core.Domain.Manifest.Subscriptions.ManifestStatusSubscription().ProcessEventsAsync(page,null!,s,default);
            page.Events=[pe];
            await new FreightOps_Core.Domain.Pros.Subscriptions.ProStatusSubscription().ProcessEventsAsync(page,null!,s,default);
            await s.SaveChangesAsync();
            var log=await s.LoadAsync<FreightOps_Core.Domain.Manifest.Projections.ManifestStatusLog>($"{me.StreamKey}-{me.Sequence}");
            check(log?.StatusEventBy=="Aurora sync","real status subscriptions process legacy machine events without user headers");
        }
        target.ManifestPublishingEnabled=false;
    }
}
