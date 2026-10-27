using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

static class ImportChecks
{
    public static async Task Run(NpgsqlConnectionFactory factory,ITenantContext tenant,Guid connectionId,Action<bool,string> check)
    {
        await using var db=await factory.OpenConnectionAsync();
        await db.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE tenant_id=@TenantId",new{tenant.TenantId});
        var entities=new TestEntities(tenant);
        var importer=new SyncImporter(factory,tenant,entities);
        ExternalChange Change(string eventId,string id,string json,string revision="r1",bool deleted=false)=>new(eventId,"fixture",id,revision,JsonNode.Parse(json)!.AsObject(),deleted);
        async Task Apply(params ExternalChange[] changes)=>await importer.Apply(connectionId,new(changes,"checkpoint"),default);
        var initial=Change("i1","external-c","""{"address":"Old","phone":"1"}""");
        await Apply(initial);
        var localId=await db.ExecuteScalarAsync<string>("SELECT local_id FROM aurora_sync_mapping WHERE entity_type='fixture' AND external_id='external-c'")??throw new Exception("Mapping missing");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM sync_fixture")==1,"incoming record creates a local record and stable mapping");
        await Apply(initial);
        check(await db.ExecuteScalarAsync<int>("SELECT revision FROM sync_fixture")==0,"replayed inbound event does not write twice");
        try{await Apply(initial with{Data=JsonNode.Parse("""{"phone":"changed"}""")!.AsObject()});throw new Exception("Reused event accepted");}
        catch(FormatException){check(true,"event ID cannot silently identify different contents");}
        await db.ExecuteAsync("UPDATE sync_fixture SET data=jsonb_set(data,'{address}','\"Local\"'),revision=1");
        await using(var tx=await db.BeginTransactionAsync()){
            await SyncStore.Enqueue(db,tx,tenant.TenantId,"fixture",localId,1,"upsert","""{"address":"Local","phone":"1"}""",default);
            await tx.CommitAsync();
        }
        await Apply(Change("i2","external-c","""{"address":"Old","phone":"2"}""","r2"));
        check(await db.ExecuteScalarAsync<string>("SELECT data->>'address'||':'||(data->>'phone') FROM sync_fixture")=="Local:2","inbound merge preserves local edit and imports independent external edit");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='fixture' AND state='superseded'")==1,"old queued snapshot superseded after merge");
        check(await db.ExecuteScalarAsync<string>("SELECT payload->>'phone' FROM aurora_sync_outbox WHERE entity_type='fixture' AND state='pending'")=="2","rebased outbound payload contains the merged external fields");
        await Apply(Change("i3","external-c","""{"address":"Remote","phone":"2"}""","r3"));
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_conflict WHERE resolved_at IS NULL")==1,"same-field concurrent edit creates durable conflict");
        check(await db.ExecuteScalarAsync<string>("SELECT data->>'address' FROM sync_fixture")=="Local","conflict preserves current local data");
        var store=new SyncStore(factory,tenant);
        // Existing test records may have due retries; isolate this assertion by leasing them first.
        while(await store.Claim(connectionId,default) is {} work){check(work.EntityType!="fixture","unresolved inbound conflict blocks outbound overwrite");await store.Complete(work,new("test-"+work.LocalId,"done"),default);}
        check(await store.Claim(connectionId,default) is null,"conflicted record is not leased");
        var before=await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_inbox");
        try{await Apply(Change("i4","new-valid","{}"),Change("i5","fails","""{"fail":true}"""));throw new Exception("Broken batch accepted");}
        catch(FormatException){check(true,"failed business import aborts batch");}
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_inbox")==before&&await db.ExecuteScalarAsync<int>("SELECT count(*) FROM sync_fixture")==1,"batch rollback removes writes, mappings and receipts together");
        await Apply(Change("i6","deleted","{}",deleted:true));
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM sync_fixture WHERE deleted")==1,"incoming deletion is retained as a tombstone");
        // Active pushes must finish before importing snapshots of the same entity.
        await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='working',lease_id=gen_random_uuid(),lease_until=now()+interval '2 minutes' WHERE entity_type='fixture' AND state='pending'");
        try{await Apply(Change("i7","external-c","{}","r4"));throw new Exception("Concurrent push ignored");}
        catch(SyncBusyException){check(true,"inbound batch defers while the same record is being pushed");}
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_inbox WHERE event_id='i7'")==0,"deferred import is not acknowledged");
        await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='conflict',lease_id=NULL,lease_until=NULL WHERE entity_type='fixture' AND state='working'");
        var review=new SyncReview(factory,tenant,entities);
        var conflicts=await review.Conflicts(default);
        check(conflicts.Length==1&&(await review.Status(default))!.Conflicts==1,"review reports one conflict per record");
        try{await review.Resolve(conflicts[0].Id,new(0,JsonNode.Parse(conflicts[0].ExternalValue)!.AsObject(),false),default);throw new Exception("Stale review accepted");}
        catch(FormatException){check(true,"conflict resolution rejects stale local revision");}
        await review.Resolve(conflicts[0].Id,new(conflicts[0].LocalRevision,JsonNode.Parse(conflicts[0].ExternalValue)!.AsObject(),false),default);
        check((await review.Conflicts(default)).Length==0&&await db.ExecuteScalarAsync<string>("SELECT data->>'address' FROM sync_fixture WHERE id=@localId",new{localId})=="Remote","accepting external values resolves conflict atomically");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='fixture' AND state IN ('pending','conflict')")==0,"accepting external values does not echo stale local writes");
        await Apply(Change("i8","external-c","""{"address":"Remote 2","phone":"2"}""","r4"));
        check(await db.ExecuteScalarAsync<string>("SELECT data->>'address' FROM sync_fixture WHERE id=@localId",new{localId})=="Remote 2","later external changes apply against resolved baseline");
        await Apply(Change("i9","deleted","{\"restored\":true}","r2"));
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM sync_fixture WHERE deleted")==0,"external restoration of an externally deleted record is not a false conflict");
        await db.ExecuteAsync("UPDATE sync_fixture SET deleted=true,revision=revision+1 WHERE id=@localId",new{localId});
        await Apply(Change("i10","external-c","""{"address":"Remote 2","phone":"2"}""","r4"));
        check(await db.ExecuteScalarAsync<bool>("SELECT deleted FROM sync_fixture WHERE id=@localId",new{localId}),"unchanged remote snapshot does not resurrect a local deletion");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE entity_type='fixture' AND local_id=@localId AND operation='delete' AND state='pending'",new{localId})==1,"local deletion remains queued after an unchanged remote snapshot");
        await Apply(Change("i11","external-c","""{"address":"Competing update","phone":"2"}""","r5"));
        check((await review.Conflicts(default)).Length==1,"remote edit competing with local deletion requires review");
    }
    sealed class TestEntities(ITenantContext tenant):ISyncEntityStore
    {
        public async Task<SyncEntity?> Read(string entityType,string localId,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
        {
            var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT id,revision,data::text AS Data,deleted FROM sync_fixture WHERE tenant_id=@TenantId AND id=@localId FOR UPDATE",new{tenant.TenantId,localId},tx,cancellationToken:ct));
            return row is null?null:new(row.Id,row.Revision,JsonNode.Parse(row.Data)!.AsObject(),row.Deleted);
        }
        public async Task<SyncEntity> Write(string entityType,string? localId,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
        {
            if(data["fail"]?.GetValue<bool>()==true)throw new FormatException("Business validation failed.");
            var id=localId??Guid.NewGuid().ToString();
            var revision=await db.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO sync_fixture(tenant_id,id,data,deleted) VALUES(@TenantId,@id,CAST(@json AS jsonb),@deleted) ON CONFLICT(tenant_id,id) DO UPDATE SET data=excluded.data,deleted=excluded.deleted,revision=sync_fixture.revision+1 RETURNING revision",new{tenant.TenantId,id,json=data.ToJsonString(),deleted},tx,cancellationToken:ct));
            return new(id,revision,data,deleted);
        }
        sealed record Row(string Id,long Revision,string Data,bool Deleted);
    }
}
