using System.Text.Json.Nodes;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Sync;
using Dapper;
using Npgsql;

static class AuthorityChecks
{
    public static async Task Run(string connection,Action<bool,string> check)
    {
        var tenant=new Context(Guid.NewGuid());var nova=Guid.NewGuid();var freight=Guid.NewGuid();
        await using var source=NpgsqlDataSource.Create(connection);
        var factory=new NpgsqlConnectionFactory(source,tenant);
        await using var db=await factory.OpenConnectionAsync();
        await db.ExecuteAsync("DROP TABLE aurora_sync_connection; CREATE TABLE tenant(id uuid PRIMARY KEY); CREATE TABLE native_cache_fixture(id text PRIMARY KEY,revision bigint,data jsonb,deleted boolean);");
        foreach(var file in new[]{"0030_integration_sync.sql","0031_sync_import.sql","0042_sync_connections.sql"})
            await db.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,file)));
        await db.ExecuteAsync("""
            INSERT INTO tenant VALUES(@TenantId);
            INSERT INTO aurora_sync_connection(tenant_id,id,provider,source_identity,credential_reference,enabled)
            VALUES(@TenantId,@nova,'nova','native',@nova,true),(@TenantId,@freight,'freightops','target',@freight,true);
            """,new{tenant.TenantId,nova,freight});
        var store=new Entities();var normal=new SyncImporter(factory,tenant,store);var direct=new SyncImporter(factory,tenant,store,true);
        var lineId=Guid.NewGuid().ToString();
        ExternalBatch Batch(string id,string value)=>new([new(id,"order","native-1",id,new JsonObject{["Value"]=value,["Lykes"]=new JsonObject{["Lines"]=new JsonArray(new JsonObject{["LineId"]=lineId,["Description"]="Native line"})}})],id);
        await normal.Apply(nova,Batch("initial","Original"),default);
        await db.ExecuteAsync("UPDATE native_cache_fixture SET data=jsonb_set(jsonb_set(data || '{\"Value\":\"Old cached edit\",\"FreightOpsState\":\"Created\"}','{Lykes,Lines,0,PO}','\"Keep target PO\"'),'{Lykes,Lines,0,Description}','\"Cached description\"'),revision=revision+1");
        await using(var tx=await db.BeginTransactionAsync())
        {
            await SyncStore.Enqueue(db,tx,tenant.TenantId,"order","native-1",2,"upsert","{\"Value\":\"Old cached edit\"}",default);
            await tx.CommitAsync();
        }
        await normal.Apply(nova,Batch("competing","Native saved edit"),default);
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_conflict WHERE resolved_at IS NULL")==1,"Local workspace still holds competing edits for review");
        await direct.Apply(nova,Batch("authoritative","Native saved edit"),default);
        check(await db.ExecuteScalarAsync<string>("SELECT data->>'Value' FROM native_cache_fixture")=="Native saved edit","Direct mode uses native saved data instead of old cache edits");
        check(await db.ExecuteScalarAsync<string>("SELECT data->>'FreightOpsState' FROM native_cache_fixture")=="Created","Native imports preserve target-owned execution metadata");
        check(await db.ExecuteScalarAsync<string>("SELECT data#>>'{Lykes,Lines,0,PO}' FROM native_cache_fixture")=="Keep target PO","Line supplements survive by stable identity");
        check(await db.ExecuteScalarAsync<string>("SELECT data#>>'{Lykes,Lines,0,Description}' FROM native_cache_fixture")=="Native line","Source line values replace old cached edits");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_conflict WHERE resolved_at IS NULL")==0,"Obsolete cache conflicts no longer block native imports");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_conflict")==1,"Prior conflict history is retained");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@nova AND state='superseded'",new{nova})==1,"Old outbound cache edit is archived without sending it to Nova");
        check(await db.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@freight AND payload->>'Value'='Native saved edit' AND state='pending'",new{freight})==1,"Native accepted change is queued for FreightOps");
        var revision=await db.ExecuteScalarAsync<long>("SELECT revision FROM native_cache_fixture");
        await direct.Apply(nova,Batch("authoritative","Native saved edit"),default);
        check(await db.ExecuteScalarAsync<long>("SELECT revision FROM native_cache_fixture")==revision,"Native cache replacement remains replay-safe");
        await db.ExecuteAsync("UPDATE aurora_sync_outbox SET state='working',lease_id=gen_random_uuid(),lease_until=now()+interval '1 minute' WHERE connection_id=@nova",new{nova});
        try{await direct.Apply(nova,Batch("inflight","Must wait"),default);throw new Exception("In-flight write bypassed");}
        catch(SyncBusyException){check(true,"Authoritative imports still wait for in-flight native writes");}
        check(await db.ExecuteScalarAsync<string>("SELECT data->>'Value' FROM native_cache_fixture")=="Native saved edit","Rejected import leaves the cache unchanged");
    }
    sealed record Context(Guid TenantId):ITenantContext{public Guid? TenantIdOrNull=>TenantId;public Guid? UserIdOrNull=>null;}
    sealed class Entities:ISyncEntityStore
    {
        public async Task<SyncEntity?> Read(string kind,string id,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
        {
            var row=await db.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT id,revision,data::text AS Json,deleted FROM native_cache_fixture WHERE id=@id FOR UPDATE",new{id},tx,cancellationToken:ct));
            return row is null?null:new(row.Id,row.Revision,JsonNode.Parse(row.Json)!.AsObject(),row.Deleted);
        }
        public async Task<SyncEntity> Write(string kind,string? id,JsonObject data,bool deleted,NpgsqlConnection db,NpgsqlTransaction tx,CancellationToken ct)
        {
            id??="native-1";
            var revision=await db.QuerySingleAsync<long>(new CommandDefinition("INSERT INTO native_cache_fixture VALUES(@id,1,CAST(@json AS jsonb),@deleted) ON CONFLICT(id) DO UPDATE SET data=excluded.data,deleted=excluded.deleted,revision=native_cache_fixture.revision+1 RETURNING revision",new{id,json=data.ToJsonString(),deleted},tx,cancellationToken:ct));
            return new(id,revision,(JsonObject)data.DeepClone(),deleted);
        }
        sealed record Row(string Id,long Revision,string Json,bool Deleted);
    }
}
