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
using Marten;

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
        options.Lykes.PilotOrderIds=[999999];
        check((await ReadNative())["Id"]!.ToString()=="910001","previously enrolled manifest members remain readable outside the current pilot list");
        options.Lykes.PilotOrderIds=[];
        await using(var duplicateTx=await pg.BeginTransactionAsync())
        {
            try{await entities.Write("order",null,first,false,pg,duplicateTx,default);throw new Exception("Unbound duplicate PRO was accepted");}
            catch(FormatException){check(true,"unbound duplicate PRO reports an identity conflict instead of overwriting the existing order");}
            await duplicateTx.RollbackAsync();
        }
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
        await freightRunner.Pull(freight,null,default);
        options.Lykes.OperationalWritesEnabled=true;
        await novaRunner.PushPending(nova,default);
        var dispatchEdit=(JsonObject)mvpRemote.Data.DeepClone();
        dispatchEdit["Notes"]="Dispatcher reverse note";dispatchEdit["Address"]="25 Dispatch Street";
        dispatchEdit["Latitude"]=30.2672;dispatchEdit["Longitude"]=-97.7431;
        dispatchEdit["AppointmentFrom"]="2026-11-03T10:00:00-05:00";
        dispatchEdit["AppointmentTo"]="2026-11-03T12:00:00-05:00";
        dispatchEdit["ScheduledAt"]="2026-11-03T10:00:00-05:00";
        using(var editing=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests("910030",mvpRemote.Revision),_=>Task.FromResult(token)))
            await editing.Push(new(Guid.NewGuid(),freight,"order","operational",1,"upsert",dispatchEdit.ToJsonString(),1,Guid.NewGuid()),default);
        await freightRunner.Pull(freight,null,default);
        check(await novaRunner.PushPending(nova,default)==1,"operational reverse order edit reaches Nova without stable native line IDs");
        check(await fb.ExecuteScalarAsync<string>("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@mvpNative",new{mvpNative})=="Dispatcher reverse note"&&
            await fb.ExecuteScalarAsync<string>("SELECT OPS_ADDR1 FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@mvpNative AND OPS_TYPE='D'",new{mvpNative})=="25 Dispatch Street",
            "reverse dispatcher notes and address persist in native fields");
        check(await fb.ExecuteScalarAsync<DateTime>("SELECT OPS_APPT_LOW FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@mvpNative AND OPS_TYPE='D'",new{mvpNative})==new DateTime(2026,11,3,10,0,0),
            "reverse appointment uses the configured Nova local timezone");
        check(await fb.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_LINE_ITEMS WHERE OPL_ORDER_ID=@mvpNative AND OPL_SYNC_ID IS NULL AND OPL_WEIGHT=20 AND OPL_PIECES=2",new{mvpNative})==1,
            "operational write leaves native freight and its missing identity untouched");
        var operationalMapping=await pg.QuerySingleAsync<TargetMapping>("SELECT external_revision AS Revision,baseline::text AS Json FROM aurora_sync_mapping WHERE connection_id=@freight AND external_id='910030'",new{freight});
        mvpRemote=mvpRemote with{Data=JsonNode.Parse(operationalMapping.Json)!.AsObject(),Revision=operationalMapping.Revision};
        var guarded=(JsonObject)mvpRemote.Data.DeepClone();guarded["Weight"]=21;
        try{LykesNovaOperationalChanges.ValidateOrder(guarded,mvpRemote.Data);throw new Exception("Operational guard accepted freight edit");}
        catch(FormatException){check(true,"operational permission does not enable freight totals edits");}
        options.Lykes.OperationalWritesEnabled=false;
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
        await ManifestPublishingChecks.Run(factory,tenant,pg,queue,importer,remote,target,targetStore,check);
        options.Lykes.NativeReadOnly=true;options.Lykes.OperationalWritesEnabled=true;options.Lykes.OperationalOrderCreationEnabled=true;
        target.AutoEnrollFromUtc=DateTime.UtcNow.AddSeconds(-1);
        var automatic=(JsonObject)data.DeepClone();automatic["Id"]="910040";
        automatic["Lykes"]!["Lines"]![0]!["LineId"]=Guid.NewGuid().ToString();
        automatic["Lykes"]!["Lines"]![0]!["Description"]="Native automatically enrolled freight";
        await using(var session=targetStore.LightweightSession())
        {
            var pro=FreightOps_Core.Domain.Pros.Pro.Factory("910040",FreightOps_Core.Domain.Pros.Enums.BillType.Standard,FreightOps_Core.Domain.Pros.Enums.RouteVia.DIRECT);
            LykesOrderMapping.Apply(pro,automatic,null,target);
            session.Events.StartStream<FreightOps_Core.Domain.Pros.Pro>(pro.Id,pro.GetUncommittedEvents());
            await session.SaveChangesAsync();
        }
        await new AuroraAutomaticEnrollment(targetStore,target).Run(default);
        await freightRunner.Pull(freight,null,default);
        check(await pg.ExecuteScalarAsync<int>("SELECT count(*) FROM aurora_order WHERE id='910040'")==1,"new native FreightOps order enrolls automatically through the real pull endpoint");
        await novaRunner.PushPending(nova,default);
        var autoNative=await fb.ExecuteScalarAsync<long?>("SELECT PB_ID FROM OPS_HEADER WHERE PBNUM=910040");
        if(autoNative is null)Console.WriteLine(JsonSerializer.Serialize(await pg.QueryAsync("SELECT state,last_error FROM aurora_sync_outbox WHERE connection_id=@nova AND payload->>'Id'='910040'",new{nova})));
        check(autoNative is >0,"automatically enrolled FreightOps order creates a new Nova order without enabling existing freight edits");
        await novaRunner.Pull(nova,null,default);await freightRunner.PushPending(freight,default);await freightRunner.Pull(freight,null,default);
        var autoBaseline=await pg.QuerySingleAsync<string>("SELECT baseline::text FROM aurora_sync_mapping WHERE connection_id=@nova AND external_id=@id AND entity_type='order'",new{nova,id=autoNative!.Value.ToString()});
        check(JsonNode.Parse(autoBaseline)!["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==automatic["Lykes"]!["Lines"]![0]!["LineId"]!.ToString(),"first native roundtrip retains FreightOps line identity instead of replacing barcodes");
        await fb.ExecuteAsync("UPDATE OPS_LINE_ITEMS SET OPL_SYNC_ID=NULL WHERE OPL_ORDER_ID=@id",new{id=autoNative});
        await using(var tx=fb.BeginTransaction())
        {
            var snapshot=(await mapper.Read(new(0,"order",autoNative.Value.ToString(),"U"),fb,tx,default)).Single().Data;
            check(snapshot["Lykes"]!["Lines"]![0]!["LineId"]!.ToString()==automatic["Lykes"]!["Lines"]![0]!["LineId"]!.ToString(),"unchanged freight retains its original identity after an old native writer drops the optional ID");
        }
        target.ManifestPublishingEnabled=true;
        const string autoManifest="FO-AUTOMATIC-PLAN";
        await using(var session=targetStore.LightweightSession())
        {
            var proStream=await session.Events.FetchForWriting<FreightOps_Core.Domain.Pros.Pro>("910040");var pro=proStream.Aggregate!;pro.ClearUncommittedEvents();
            var manifest=FreightOps_Core.Domain.Manifest.Manifest.Factory(autoManifest,FreightOps_Core.Domain.Manifest.Enums.ManifestTemplate.Delivery.Name);
            manifest.UpdateOriginTerminal(target.NativeTerminal("AUS"));manifest.UpdateXref("native-user-reference");
            var nativeQueue=pro.LineItems.SelectMany(l=>l.TrackingUnits.Select(t=>new FreightOps_Core.Domain.Manifest.LineitemQueue{
                Barcode=t.Barcode,ProNumber=pro.Id,ManifestId=autoManifest,Shipper=pro.Shipper?.Name??"",ShipperConsignee=pro.ShipperConsignee?.Name??"",CarrierConsignee=pro.CarrierConsignee.Name,carrierConsigneePostalCode=pro.CarrierConsignee.Address.PostalCode,
                Description=l.Sku.Description,Sku=l.Sku.Sku,Location=t.Location,DefaultLocation=pro.DefaultLocation,Status=FreightOps_Core.Domain.Manifest.Enums.ProcessingStatus.Planning})).ToList();
            manifest.AssignPro(new FreightOps_Core.Domain.Manifest.ManPro(pro.Id,pro.Units,pro.Weight,pro.Cubes,pro.Seats,pro.Revenue,pro.CarrierConsignee.Address),nativeQueue);
            manifest.RecordAuroraPlan(DateTime.UtcNow,"{}","Native auto plan");pro.AssignToManifest(autoManifest,true);
            proStream.AppendMany(pro.GetUncommittedEvents());session.Events.StartStream<FreightOps_Core.Domain.Manifest.Manifest>(autoManifest,manifest.GetUncommittedEvents());await session.SaveChangesAsync();
        }
        await freightRunner.Pull(freight,null,default);
        var automaticManifest=await pg.QuerySingleAsync<AutoManifest>("SELECT local_id AS LocalId,external_revision AS Revision,baseline::text AS Json FROM aurora_sync_mapping WHERE connection_id=@freight AND entity_type='manifest' AND external_id=@autoManifest",new{freight,autoManifest});
        check(JsonNode.Parse(automaticManifest.Json)!["Orders"]!.AsArray().Count==1,"new native FreightOps manifest automatically enrolls after its order has a workspace binding");
        var manifestEdit=JsonNode.Parse(automaticManifest.Json)!.AsObject();manifestEdit["Details"]!["Notes"]="Updated auto plan";
        var member=manifestEdit["Orders"]![0]!;
        await using(var query=targetStore.QuerySession())
            manifestEdit["FreightOpsOrders"]=new JsonArray(new JsonObject{["LocalId"]=member["SourceId"]!.ToString()+"/"+member["Id"]!.ToString(),["ExternalId"]="910040",["ExpectedRevision"]=(await query.Events.FetchStreamStateAsync("910040"))!.Version.ToString()});
        using(var editing=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests(autoManifest,automaticManifest.Revision),_=>Task.FromResult(token)))
            await editing.Push(new(Guid.NewGuid(),freight,"manifest",automaticManifest.LocalId,1,"upsert",manifestEdit.ToJsonString(),1,Guid.NewGuid()),default);
        await freightRunner.Pull(freight,null,default);
        await using(var query=targetStore.QuerySession())
            check((await query.LoadAsync<FreightOps_Core.Domain.Manifest.Manifest>(autoManifest))!.Xref=="native-user-reference","automatic manifest binding preserves the native user's reference");
        // Real native loading commands establish prerequisites; execution sync never invents scans.
        await using(var query=targetStore.QuerySession())
        {
            var pro=(await query.LoadAsync<FreightOps_Core.Domain.Pros.Pro>("910040"))!;
            var link=(await query.LoadAsync<SyncLink>(target.ConnectionId.ToString("N")+"/910040"))!;
            var incoming=JsonNode.Parse(link.Canonical)!.AsObject();incoming["NativeOrderId"]=autoNative.Value;
            var unbound=(JsonObject)incoming.DeepClone();unbound.Remove("NativeOrderId");unbound.Remove("Country");incoming["Country"]="";
            unbound["Lykes"]!.AsObject().Remove("IdentityMode");unbound["Lykes"]!.AsObject().Remove("FreightFingerprint");
            foreach(var row in unbound["Lykes"]!["Lines"]!.AsArray()){row!.AsObject().Remove("Nova");row.AsObject().Remove("Keyword");}
            var barcodes=pro.LineItems.SelectMany(x=>x.TrackingUnits).Select(x=>x.Barcode).ToArray();
            LykesOrderMapping.Apply(pro,incoming,unbound,target);
            check(barcodes.SequenceEqual(pro.LineItems.SelectMany(x=>x.TrackingUnits).Select(x=>x.Barcode)),"late first Nova identity binding preserves assigned physical freight and allows empty optional defaults");
            incoming["Lykes"]!["Lines"]![0]!["Pieces"]=99;
            try{LykesOrderMapping.Apply(pro,incoming,unbound,target);throw new Exception("Assigned physical change accepted");}catch(SyncTargetConflict){check(true,"first identity binding cannot bypass assigned freight protection");}
        }
        await using(var session=targetStore.LightweightSession())
        {
            var stream=await session.Events.FetchForWriting<FreightOps_Core.Domain.Manifest.Manifest>(autoManifest);var m=stream.Aggregate!;m.ClearUncommittedEvents();
            var dispatched=(JsonObject)manifestEdit.DeepClone();dispatched["Status"]="Dispatched";dispatched["Details"]!["StatusOccurredAtUtc"]=DateTimeOffset.UtcNow.ToString("O");
            try{AuroraManifestExecution.Apply(m,dispatched,manifestEdit);throw new Exception("Unloaded dispatch accepted");}catch(SyncTargetConflict){check(true,"execution synchronization holds dispatch until native loading finishes");}
            m.ReleaseOutbound("TEST-DOOR","TEST-TRAILER");
            foreach(var barcode in m.Queue.QueuedItems.Values.Select(x=>x.Barcode).ToArray())m.LoadItem(barcode,"","TEST-TRAILER","Integration test");
            dispatched["Details"]!["StatusOccurredAtUtc"]=DateTimeOffset.UtcNow.ToString("O");
            AuroraManifestExecution.Apply(m,dispatched,manifestEdit);
            check(m.TransitStatus==FreightOps_Core.Domain.Manifest.Enums.ManifestStatus.Closed,"loaded manifest dispatch uses native close command");
            var count=m.GetUncommittedEvents().Count();AuroraManifestExecution.Apply(m,dispatched,manifestEdit);
            check(m.GetUncommittedEvents().Count()==count,"repeated dispatch does not duplicate native events");
            var departed=(JsonObject)dispatched.DeepClone();departed["Status"]="En Route";departed["Details"]!["StatusOccurredAtUtc"]=DateTimeOffset.UtcNow.ToString("O");
            AuroraManifestExecution.Apply(m,departed,dispatched);
            check(m.TransitStatus==FreightOps_Core.Domain.Manifest.Enums.ManifestStatus.InTransit,"departure synchronization uses actual source event time and native transition");
            var complete=(JsonObject)departed.DeepClone();complete["Status"]="Complete";complete["Details"]!["StatusOccurredAtUtc"]=DateTimeOffset.UtcNow.ToString("O");
            try{AuroraManifestExecution.Apply(m,complete,departed);throw new Exception("Unprocessed completion accepted");}catch(SyncTargetConflict){check(true,"completion cannot skip native unloading and return processing");}
            stream.AppendMany(m.GetUncommittedEvents());session.SetHeader("user","Integration test");await session.SaveChangesAsync();
        }
        target.DispatchSynchronizationEnabled=true;
        await using(var query=targetStore.QuerySession())
        {
            var link=await query.Query<SyncManifestLink>().Where(x=>x.ManifestId==autoManifest).SingleOrDefaultAsync();
            var status=(JsonObject)JsonNode.Parse(link.Canonical)!;status["Status"]="En Route";status["Details"]!["StatusOccurredAtUtc"]=DateTimeOffset.UtcNow.ToString("O");
            var revision=(await query.Events.FetchStreamStateAsync(autoManifest))!.Version.ToString();
            using var publishing=new FreightOpsSyncProvider(freight,target.SourceIdentity,endpoint,new Requests(autoManifest,revision),_=>Task.FromResult(token));
            var change=new SyncChange(Guid.NewGuid(),freight,"manifest",automaticManifest.LocalId,2,"upsert",status.ToJsonString(),1,Guid.NewGuid());
            var receipt=await publishing.Push(change,default);
            check(receipt==await publishing.Push(change,default),"execution endpoint returns its original receipt on response-loss retry");
        }
        var executionBatch=await remote.Pull(null,default);var execution=executionBatch.Changes.Single(x=>x.EntityType=="manifest"&&x.ExternalId==autoManifest);
        check(execution.Data["Status"]!.ToString()=="En Route"&&execution.Data["Details"]?["StatusOccurredAtUtc"] is not null,"native manifest execution and actual timestamp return through the real endpoint");
        await remote.Acknowledge(executionBatch,default);target.DispatchSynchronizationEnabled=false;
        target.AutoEnrollFromUtc=null;target.ManifestPublishingEnabled=false;
    }
    sealed record TargetMapping(string Revision,string Json);
    sealed record AutoManifest(string LocalId,string Revision,string Json);
    sealed record Context(Guid TenantId):ITenantContext{public Guid? TenantIdOrNull=>TenantId;public Guid? UserIdOrNull=>null;}
}
