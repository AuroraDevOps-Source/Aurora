using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Integrations.FreightOps;
using Aurora.Integrations.Nova;
using Aurora.Integrations.Nova.Sync;
using Aurora.Modules.Routing;
using Aurora.Modules.Sync;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using FreightOps_Core.Domain.Pros.Integration;
using Microsoft.Extensions.Options;
using Npgsql;

// Real production mapper, operational order store, importer, outbox, durable
// requests and HTTP endpoints. Only schema/data and runner scheduling are fixtures.
static class ProductionMappingChecks
{
    public static async Task Run(FbConnection fb,string firebird,string postgres,Uri endpoint,AuroraSyncOptions target,string token,Action<bool,string> check,Marten.IDocumentStore targetStore)
    {
        var tenant=new Context(Guid.NewGuid());var nova=Guid.NewGuid();
        target.ConnectionId=Guid.NewGuid();target.SourceIdentity="production-path-fixture";
        var freight=target.ConnectionId;
        await using var source=NpgsqlDataSource.Create(postgres);
        var factory=new NpgsqlConnectionFactory(source,tenant);
        await using var pg=await factory.OpenConnectionAsync();
        await pg.ExecuteAsync("""
            CREATE TABLE tenant(id uuid PRIMARY KEY);
            CREATE TABLE aurora_order_source(tenant_id uuid,id uuid,name text,request jsonb,PRIMARY KEY(tenant_id,id),UNIQUE(tenant_id,name));
            CREATE TABLE aurora_order(tenant_id uuid,source_id uuid,id text,scheduled_at timestamptz,customer text,city text,status text,
              terminal_id uuid,customer_id uuid,details jsonb,deleted_at timestamptz,revision integer NOT NULL DEFAULT 0,PRIMARY KEY(tenant_id,source_id,id));
            CREATE TABLE aurora_terminal(tenant_id uuid,id uuid,code text,deleted_at timestamptz);
            CREATE TABLE aurora_customer(tenant_id uuid,id uuid,code text,deleted_at timestamptz);
            CREATE TABLE aurora_manifest_order(tenant_id uuid,source_id uuid,order_id text);
            CREATE TABLE aurora_routing_status_configuration(tenant_id uuid,source_key text,ready_statuses integer[],pickup_ready_statuses integer[]);
            CREATE TABLE aurora_order_routing(tenant_id uuid,source_key text,order_id bigint,receiving_terminal text,revision integer);
            """);
        foreach(var script in new[]{"0030_integration_sync.sql","0031_sync_import.sql","0036_sync_runtime.sql","0039_sync_source_metadata.sql","0042_sync_connections.sql","0043_freightops_sync_requests.sql","0044_freightops_runtime.sql"})
            await pg.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations",script)));
        await pg.ExecuteAsync("""
            INSERT INTO tenant VALUES(@TenantId);
            INSERT INTO aurora_sync_connection(tenant_id,id,provider,source_identity,credential_reference,enabled)
            VALUES(@TenantId,@nova,'nova','native-fixture',@nova,true),(@TenantId,@freight,'freightops',@identity,@freight,true);
            """,new{tenant.TenantId,nova,freight,identity=target.SourceIdentity});
        // The small existing embedded fixture lacked ACCOUNT, which the real reader joins through Aurora only.
        await fb.ExecuteAsync("CREATE TABLE ACCOUNT(AC_CUST VARCHAR(12) PRIMARY KEY)");
        await fb.ExecuteAsync("INSERT INTO AURORA_SYNC_CONSUMER VALUES(@consumer,'Y')",new{consumer=nova.ToString()});
        var options=new FirebirdRoutingOptions{Enabled=true,TenantId=tenant.TenantId,ConnectionString=firebird,SourceKey="native-fixture",ReadyStatus=110,TimeZoneId="America/New_York",
            Lykes=new(){Enabled=true,WritesEnabled=true,OrderLifecycleEnabled=true,OperatorInitials="TEST",NewFreightKeyword="FRT",TerminalMappings=new(){{"N-AUS","AUS"}},KeywordKinds=new(){{"FRT","freight"}},PackTypes=new(){{"CTN","C"}},CubeBasis="per-piece",ScalarCubeUnit="ft3",DimensionUnit="in",FreightStopKind="P"}};
        check(SyncJson.Equal(JsonValue.Create("2026-10-28T10:00:00.1234567-04:00"),JsonValue.Create("2026-10-28T14:00:00.123456Z"),"ScheduledAt"),"sub-microsecond database normalization is not a local schedule edit");
        check(!SyncJson.Equal(JsonValue.Create("2026-10-28T10:00:00.1234567-04:00"),JsonValue.Create("2026-10-28T14:00:00.123457Z"),"ScheduledAt"),"a real microsecond schedule change remains distinct");
        var data=JsonSerializer.SerializeToNode(new TmsOrder{Id="910001",Customer="Receiver",Shipper="Sender",TerminalCode="AUS",ScheduledAt=DateTimeOffset.Parse("2026-10-28T10:00:00.1234567-04:00"),
            Pieces=2,Weight=20,Cube=3,Notes="Native original",Address="20 Main",City="Austin",State="TX",PostalCode="78701",OriginAddress="10 Main",OriginCity="Austin",OriginState="TX",OriginPostalCode="78701"})!.AsObject();
        var lineId=Guid.NewGuid().ToString();
        data["Lykes"]=new JsonObject{["Version"]=2,["WeightUnit"]="lb",["VolumeUnit"]="ft3",["Lines"]=new JsonArray(new JsonObject{
            ["LineId"]=lineId,["Kind"]="freight",["Sku"]="PRODUCTION-SKU",["PackType"]="C",["Pieces"]=2,["TotalWeight"]=20,["CubePerPiece"]=1.5m})};
        long nativeId;
        await using(var tx=fb.BeginTransaction())
        {
            nativeId=await LykesNovaOrderWriter.Create(fb,tx,data,options.Lykes,[110],nova,Guid.NewGuid(),TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId),default);
            await tx.CommitAsync();
        }
        var mapper=new FirebirdRoutingSource(factory,tenant,Options.Create(options)).ForSynchronization(nova);
        var native=new NovaSyncProvider(nova,firebird,mapper);
        var entities=new OperationalSyncEntities(tenant,new DriverStore(factory,tenant));
        var importer=new SyncImporter(factory,tenant,entities);var queue=new SyncStore(factory,tenant);
        var novaRunner=new SyncRunner(queue,native,importer);
        using var remote=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new FreightOpsRequestStore(factory,tenant),_=>Task.FromResult(token));
        var freightRunner=new SyncRunner(queue,remote,importer);
        async Task QueueNative()=>await fb.ExecuteAsync("INSERT INTO AURORA_SYNC_EVENT VALUES(GEN_ID(AURORA_SYNC_EVENT_GEN,1),@consumer,'order',@id,'U')",new{consumer=nova.ToString(),id=nativeId.ToString()});
        async Task<JsonObject> ReadNative()
        {
            await using var tx=fb.BeginTransaction();
            return (await mapper.Read(new(0,"order",nativeId.ToString(),"U"),fb,tx,default)).Single().Data;
        }
        async Task RejectRead(string label)
        {
            try{await ReadNative();throw new Exception("Expected identity hold: "+label);}
            catch(Exception e) when(e is FormatException or SyncConflictException){check(true,label);}
        }
        var first=await ReadNative();
        check(first["Id"]!.GetValue<string>()=="910001"&&first["NativeOrderId"]!.GetValue<long>()==nativeId,"production Nova reader separates business PRO from database key");
        options.Lykes.Enabled=false;
        check((await ReadNative())["Id"]!.GetValue<string>()==$"FB-{nativeId}","legacy non-Lykes import retains FB identity");options.Lykes.Enabled=true;
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PBSUFFIX='A' WHERE PB_ID=@nativeId",new{nativeId});await RejectRead("suffixed PRO is held, never silently stripped");
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PBSUFFIX='' WHERE PB_ID=@nativeId",new{nativeId});
        foreach(var invalid in new[]{"NULL","0","-1"})
        {
            await fb.ExecuteAsync($"UPDATE OPS_HEADER SET PBNUM={invalid} WHERE PB_ID=@nativeId",new{nativeId});
            await RejectRead("invalid native PRO "+invalid+" is held before import");
        }
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PBNUM=910001 WHERE PB_ID=@nativeId",new{nativeId});
        await fb.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PBNUM,PBLOCAT,PB_DT_CANC) VALUES(999999,'O',910001,'OTHER',CURRENT_TIMESTAMP)");
        await RejectRead("duplicate PRO at another location including cancelled history is held");
        await fb.ExecuteAsync("DELETE FROM OPS_HEADER WHERE PB_ID=999999");
        await novaRunner.Pull(nova,null,default);
        var local=await pg.QuerySingleAsync<string>("SELECT local_id FROM aurora_sync_mapping WHERE connection_id=@nova",new{nova});
        check(local.EndsWith("/910001")&&await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_order WHERE id='910001'")==1,"production importer stores business PRO and native mapping separately");
        check(await freightRunner.PushPending(freight,default)==1,"production outbox and durable request deliver native PRO to real FreightOps endpoint");
        var remoteBatch=await remote.Pull(null,default);
        check(remoteBatch.Changes.Single().ExternalId=="910001"&&remoteBatch.Changes.Single().Data["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==lineId,"FreightOps receives business PRO and original stable line identity");
        await freightRunner.Pull(freight,null,default);
        // FreightOps adds empty metadata and Created state; synchronize that baseline first.
        await novaRunner.PushPending(nova,default);
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE state IN ('pending','working','conflict')")==0,"initial production round trip settles without conflicts");
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PB_NOTES='Native revised' WHERE PB_ID=@nativeId",new{nativeId});await QueueNative();
        await novaRunner.Pull(nova,null,default);
        check(await freightRunner.PushPending(freight,default)==1,"native note edit forwards through production importer and outbox");
        remoteBatch=await remote.Pull(null,default);
        check(remoteBatch.Changes.Single().Data["Notes"]!.ToString()=="Native revised","production reader refreshes native notes in FreightOps");
        await freightRunner.Pull(freight,null,default);
        var edit=(JsonObject)remoteBatch.Changes.Single().Data.DeepClone();
        edit["Pieces"]=3;edit["Weight"]=30;edit["Cube"]=4.5m;
        edit["Lykes"]!["Lines"]![0]!["Pieces"]=3;edit["Lykes"]!["Lines"]![0]!["TotalWeight"]=30;
        // Create an actual FreightOps edit through its HTTP endpoint; reverse ingestion uses the real provider.
        using(var editing=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests("910001",remoteBatch.Changes.Single().Revision),_=>Task.FromResult(token)))
            await editing.Push(new(Guid.NewGuid(),freight,"order",local,1,"upsert",edit.ToJsonString(),1,Guid.NewGuid()),default);
        await freightRunner.Pull(freight,null,default);
        check(await novaRunner.PushPending(nova,default)==1,"FreightOps correction crosses production Nova mapping lookup and guarded push");
        check(await fb.ExecuteScalarAsync<decimal>("SELECT OPL_PIECES FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@lineId",new{lineId})==3,"production reverse write changes the original Nova line");
        await novaRunner.Pull(nova,null,default);await freightRunner.PushPending(freight,default);await freightRunner.Pull(freight,null,default);
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE state IN ('pending','working','conflict')")==0,"production normalization converges without a sync loop");
        var baseline=await pg.QuerySingleAsync<string>("SELECT baseline::text FROM aurora_sync_mapping WHERE connection_id=@nova",new{nova});
        await pg.ExecuteAsync("UPDATE aurora_sync_mapping SET baseline=jsonb_set(baseline,'{Id}',to_jsonb(@oldId::text)) WHERE connection_id=@nova",new{nova,oldId=$"FB-{nativeId}"});
        await RejectRead("pre-existing FB mapping is held without automatic rename");
        check(await pg.ExecuteScalarAsync<string>("SELECT baseline->>'Id' FROM aurora_sync_mapping WHERE connection_id=@nova",new{nova})==$"FB-{nativeId}","identity hold leaves existing mapping unchanged");
        await pg.ExecuteAsync("UPDATE aurora_sync_mapping SET baseline=CAST(@baseline AS jsonb) WHERE connection_id=@nova",new{nova,baseline});
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PBNUM=910002 WHERE PB_ID=@nativeId",new{nativeId});await RejectRead("native PRO renumbering cannot rename an established mapping");
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PBNUM=910001 WHERE PB_ID=@nativeId",new{nativeId});

        // Reverse-origin creation through both real importers, including native identity binding.
        var reverse=(JsonObject)data.DeepClone();reverse["Id"]="910010";reverse["Notes"]="FreightOps origin";
        reverse["Lykes"]!["Lines"]![0]!["LineId"]=Guid.NewGuid().ToString();
        using(var creator=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests(),_=>Task.FromResult(token)))
            await creator.Push(new(Guid.NewGuid(),freight,"order","origin-910010",1,"upsert",reverse.ToJsonString(),1,Guid.NewGuid()),default);
        var originating=(await remote.Pull(null,default)).Changes.Single(x=>x.ExternalId=="910010").Data;
        check(!originating.ContainsKey("SourceId")&&!originating.ContainsKey("Revision")&&!originating.ContainsKey("ManifestId"),"FreightOps omits Aurora-local bookkeeping from canonical snapshots");
        await freightRunner.Pull(freight,null,default);
        check(await novaRunner.PushPending(nova,default)==1,"FreightOps-origin creation traverses production importer and Nova create dispatch");
        var createdId=await fb.QuerySingleAsync<long>("SELECT PB_ID FROM OPS_HEADER WHERE PBNUM=910010");
        var acknowledged=await pg.QuerySingleAsync<SyncChange>("""
            SELECT id,connection_id AS ConnectionId,entity_type AS EntityType,local_id AS LocalId,
              local_revision AS LocalRevision,operation,payload::text AS Payload,attempts,gen_random_uuid() AS LeaseId
            FROM aurora_sync_outbox WHERE connection_id=@nova AND payload->>'Id'='910010' AND state='synced'
            """,new{nova});
        check((await native.Push(acknowledged,default)).ExternalId==createdId.ToString()&&await fb.ExecuteScalarAsync<int>("SELECT count(*) FROM OPS_HEADER WHERE PBNUM=910010")==1,"production create retry returns receipt without duplicating the order");
        await novaRunner.Pull(nova,null,default);
        var bindingCount=await freightRunner.PushPending(freight,default);
        check(bindingCount==1,"production normalization binds newly created Nova ID back to FreightOps");
        var bound=(await remote.Pull(null,default)).Changes.Single(x=>x.ExternalId=="910010");
        check(bound.Data["NativeOrderId"]!.GetValue<long>()==createdId&&bound.Data["Id"]!.ToString()=="910010","reverse-origin creation retains business PRO after native identity binding");
        await freightRunner.Pull(freight,null,default);
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE state IN ('pending','working','conflict')")==0,"reverse-origin production creation converges without pending work");
        using(var cancelling=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests("910010",bound.Revision),_=>Task.FromResult(token)))
            await cancelling.Push(new(Guid.NewGuid(),freight,"order","origin-910010",2,"delete",bound.Data.ToJsonString(),1,Guid.NewGuid()),default);
        await freightRunner.Pull(freight,null,default);
        check(await novaRunner.PushPending(nova,default)==1,"cancellation traverses production mapper and guarded Nova cancellation dispatch");
        check(await fb.ExecuteScalarAsync<int>("SELECT PB_STATUS FROM OPS_HEADER WHERE PB_ID=@createdId",new{createdId})==999&&
            await fb.ExecuteScalarAsync<int>("SELECT count(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@createdId",new{createdId})==1,"production cancellation preserves native freight history");
        await novaRunner.Pull(nova,null,default);
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE state IN ('pending','working','conflict')")==0,"production cancellation tombstone settles without resurrection or echo");

        var protectedMapping=await pg.QuerySingleAsync<TargetMapping>("SELECT external_revision AS Revision,baseline::text AS Json FROM aurora_sync_mapping WHERE connection_id=@freight AND external_id='910001'",new{freight});
        var protectedEdit=JsonNode.Parse(protectedMapping.Json)!.AsObject();
        protectedEdit["Weight"]=40;protectedEdit["Lykes"]!["Lines"]![0]!["TotalWeight"]=40;
        await fb.ExecuteAsync("UPDATE OPS_HEADER SET PB_INV_DATE=CURRENT_DATE WHERE PB_ID=@nativeId",new{nativeId});
        using(var editing=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests("910001",protectedMapping.Revision),_=>Task.FromResult(token)))
            await editing.Push(new(Guid.NewGuid(),freight,"order",local,3,"upsert",protectedEdit.ToJsonString(),1,Guid.NewGuid()),default);
        var receipts=await fb.ExecuteScalarAsync<int>("SELECT count(*) FROM AURORA_SYNC_RECEIPT WHERE CONSUMER_ID=@consumer",new{consumer=nova.ToString()});
        await freightRunner.Pull(freight,null,default);
        check(await novaRunner.PushPending(nova,default)==0&&await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@nova AND state='conflict'",new{nova})==1,"production reverse push holds posted freight for review");
        check(await fb.ExecuteScalarAsync<decimal>("SELECT OPL_WEIGHT FROM OPS_LINE_ITEMS WHERE OPL_SYNC_ID=@lineId",new{lineId})==30&&
            await fb.ExecuteScalarAsync<int>("SELECT count(*) FROM AURORA_SYNC_RECEIPT WHERE CONSUMER_ID=@consumer",new{consumer=nova.ToString()})==receipts,"posted-freight rejection changes neither native measurements nor receipts");

        var scheduled=(JsonObject)data.DeepClone();scheduled["Id"]="910020";scheduled["Lykes"]!["Lines"]![0]!["LineId"]=Guid.NewGuid().ToString();
        await using(var tx=fb.BeginTransaction())
        {
            await LykesNovaOrderWriter.Create(fb,tx,scheduled,options.Lykes,[110],nova,Guid.NewGuid(),TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId),default);
            await tx.CommitAsync();
        }
        await novaRunner.Pull(nova,null,default);
        var runtime=new FreightOpsRuntimeOptions{Connections=[new(){TenantId=tenant.TenantId,CredentialReference=freight,ApiUrl=endpoint.ToString(),SourceIdentity=target.SourceIdentity,AccessToken=token}]};
        await FreightOpsSyncCycle.Run(factory,tenant,runtime,entities,default);
        check(await pg.ExecuteScalarAsync<DateTime?>("SELECT last_attempt_at FROM aurora_sync_connection WHERE id=@freight",new{freight}) is null,"FreightOps runtime defaults off and makes no attempt");
        runtime.Enabled=true;
        await pg.ExecuteAsync("SELECT pg_advisory_lock(hashtextextended(@key,0))",new{key=$"aurora-background-sync:{tenant.TenantId}"});
        try
        {
            await FreightOpsSyncCycle.Run(factory,tenant,runtime,entities,default);
            check(await pg.ExecuteScalarAsync<DateTime?>("SELECT last_attempt_at FROM aurora_sync_connection WHERE id=@freight",new{freight}) is null,"runtime respects the shared per-tenant worker lock");
        }
        finally{await pg.ExecuteAsync("SELECT pg_advisory_unlock(hashtextextended(@key,0))",new{key=$"aurora-background-sync:{tenant.TenantId}"});}
        await FreightOpsSyncCycle.Run(factory,tenant,runtime,entities,default);
        check(await pg.ExecuteScalarAsync<DateTime?>("SELECT initialized_at FROM aurora_sync_connection WHERE id=@freight",new{freight}) is not null,"enabled runtime executes the production HTTP/import/outbox cycle");
        check((await remote.Pull(null,default)).Changes.Any(x=>x.ExternalId=="910020"),"runtime delivers a queued native order to FreightOps");
        runtime.Connections[0].AccessToken="invalid-local-fixture-token";
        try{await FreightOpsSyncCycle.Run(factory,tenant,runtime,entities,default);throw new Exception("Bad runtime credential accepted");}
        catch(HttpRequestException){check(true,"runtime rejects invalid machine credentials");}
        var error=await pg.ExecuteScalarAsync<string>("SELECT last_error FROM aurora_sync_connection WHERE id=@freight",new{freight});
        check(error is not null&&!error.Contains("invalid-local-fixture-token"),"runtime failure status contains no token");
        runtime.Connections[0].AccessToken=token;runtime.Connections[0].SourceIdentity="different-generation";
        try{await FreightOpsSyncCycle.Run(factory,tenant,runtime,entities,default);throw new Exception("Generation changed");}
        catch(FormatException){check(true,"runtime refuses a different source generation");}
        runtime.Connections[0].SourceIdentity=target.SourceIdentity;
        try{runtime.Resolve(Guid.NewGuid(),freight);throw new Exception("Foreign tenant resolved");}
        catch(FormatException){check(true,"runtime credentials are tenant-scoped");}
        await FreightOpsSyncCycle.Run(factory,tenant,runtime,entities,default);
        check(await pg.ExecuteScalarAsync<string>("SELECT last_error FROM aurora_sync_connection WHERE id=@freight",new{freight}) is null,"successful runtime recovery clears its own connection error");
        await HostMappingChecks.Run(source,factory,tenant,entities,runtime,targetStore,check);
        // MVP uses unchanged native clients: no OPL_SYNC_ID survives their saves.
        await pg.ExecuteAsync("UPDATE aurora_sync_connection SET enabled=true WHERE id=@nova",new{nova});
        var mvpSeed=(JsonObject)data.DeepClone();mvpSeed["Id"]="910030";mvpSeed["Lykes"]!["Lines"]![0]!["LineId"]=Guid.NewGuid().ToString();
        long mvpNative;
        await using(var tx=fb.BeginTransaction())
        {
            mvpNative=await LykesNovaOrderWriter.Create(fb,tx,mvpSeed,options.Lykes,[110],nova,Guid.NewGuid(),TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId),default);
            await tx.CommitAsync();
        }
        await fb.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_SYNC_ID=NULL WHERE OPL_ORDER_ID=@mvpNative",new{mvpNative});
        options.Lykes.NativeReadOnly=true;
        await novaRunner.Pull(nova,null,default);
        check(await freightRunner.PushPending(freight,default)==1,"MVP native freight without native line IDs reaches real FreightOps");
        var mvpRemote=(await remote.Pull(null,default)).Changes.Single(x=>x.ExternalId=="910030");
        check(mvpRemote.Data["Lykes"]!["IdentityMode"]!.ToString()=="aurora-snapshot","FreightOps preserves the MVP ownership marker");
        var mvpLineId=mvpRemote.Data["Lykes"]!["Lines"]![0]!["LineId"]!.ToString();
        await fb.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_SEQUENCE=50,OPL_AMOUNT=75 WHERE OPL_ORDER_ID=@mvpNative",new{mvpNative});
        await using(var tx=fb.BeginTransaction())
        {
            var read=(await mapper.Read(new(0,"order",mvpNative.ToString(),"U"),fb,tx,default)).Single().Data;
            check(read["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==mvpLineId,"production MVP read preserves pieces across native resequence and rerating");
        }
        var mvpEdit=(JsonObject)mvpRemote.Data.DeepClone();mvpEdit["Weight"]=22;mvpEdit["Lykes"]!["Lines"]![0]!["TotalWeight"]=22;
        using(var editing=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests("910030",mvpRemote.Revision),_=>Task.FromResult(token)))
            await editing.Push(new(Guid.NewGuid(),freight,"order","mvp",1,"upsert",mvpEdit.ToJsonString(),1,Guid.NewGuid()),default);
        await freightRunner.Pull(freight,null,default);
        await novaRunner.PushPending(nova,default);
        check(await fb.ExecuteScalarAsync<decimal>("SELECT OPL_WEIGHT FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@mvpNative",new{mvpNative})==20&&
            await fb.ExecuteScalarAsync<decimal>("SELECT OPL_AMOUNT FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@mvpNative",new{mvpNative})==75,"MVP reverse correction leaves Nova measurements and rating untouched");
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@nova AND payload->>'Id'='910030' AND state='conflict'",new{nova})==1,"MVP reverse correction is visibly held for review");
        await fb.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_PIECES=3 WHERE OPL_ORDER_ID=@mvpNative",new{mvpNative});
        await using(var tx=fb.BeginTransaction())
        {
            try{await mapper.Read(new(0,"order",mvpNative.ToString(),"U"),fb,tx,default);throw new Exception("MVP accepted changed piece association");}
            catch(FormatException){check(true,"production MVP holds changed native pieces without inventing replacement barcodes");}
        }
        // A normal order-editor save must retain the integration-only extension.
        // The earlier direct Pull deliberately left an immutable delivery pending;
        // collect the next native version after that delivery was acknowledged.
        await freightRunner.Pull(freight,null,default);
        await pg.ExecuteAsync("ALTER TABLE aurora_manifest_order ADD COLUMN IF NOT EXISTS manifest_id uuid");
        var savedJson=await pg.QuerySingleAsync<string>("SELECT (details || jsonb_build_object('SourceId',source_id,'Revision',revision))::text FROM aurora_order WHERE id='910030'");
        var beforeEdit=JsonNode.Parse(savedJson)!.AsObject();
        var editorOrder=JsonSerializer.Deserialize<TmsOrder>(savedJson)!;editorOrder.Notes="Edited through the regular Aurora order editor";
        editorOrder.CustomerCode=null;
        editorOrder.Latitude=28.01619;editorOrder.Longitude=-82.42713;editorOrder.ScheduledAt=DateTimeOffset.Parse("2026-11-02T09:00:00-05:00");
        await new TmsStore(factory,tenant).SaveOrder(editorOrder,false,default);
        var afterEdit=JsonNode.Parse(await pg.QuerySingleAsync<string>("SELECT details::text FROM aurora_order WHERE id='910030'"))!;
        check(JsonNode.DeepEquals(beforeEdit["Lykes"],afterEdit["Lykes"])&&afterEdit["Notes"]!.ToString()==editorOrder.Notes,"ordinary Aurora order edit preserves stable line metadata and updates editable fields");
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_sync_outbox WHERE connection_id=@freight AND payload->>'Id'='910030' AND payload->'Lykes' IS NOT NULL AND payload->>'Notes'=@Notes",new{freight,editorOrder.Notes})==1,"order editor enqueues complete Lykes payload for FreightOps");
        var editedPushes=await freightRunner.PushPending(freight,default);
        if(editedPushes!=1)Console.WriteLine(JsonSerializer.Serialize(await pg.QueryAsync("SELECT state,last_error,payload->>'Id' AS id FROM aurora_sync_outbox WHERE connection_id=@freight AND state NOT IN ('synced','superseded')",new{freight})));
        check(editedPushes==1,"Aurora geocoding and scheduling edits cross the real FreightOps endpoint");
        var planningReturn=(await remote.Pull(null,default)).Changes.Single(x=>x.ExternalId=="910030");
        check(planningReturn.Data["Latitude"]!.GetValue<double>()==editorOrder.Latitude&&planningReturn.Data["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==mvpLineId,"FreightOps roundtrip retains planning coordinates and stable line identity");
        await freightRunner.Pull(freight,null,default);
        await ManifestPublishingChecks.Run(factory,tenant,pg,queue,remote,target,targetStore,check);
    }
    sealed record TargetMapping(string Revision,string Json);
    sealed record Context(Guid TenantId):ITenantContext{public Guid? TenantIdOrNull=>TenantId;public Guid? UserIdOrNull=>null;}
}
