using Aurora.Integrations.Nova;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.Options;
using Npgsql;

int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
async Task Reject(Func<Task> action,string fragment){try{await action();}catch(FormatException e)when(e.Message.Contains(fragment)){Check(true,"rejects "+fragment);return;}throw new Exception("Expected rejection: "+fragment);}
var receipt="Aurora routing export v1 "+new string('A',64);
var notes=ManifestNotes.SetPlanningLock(receipt+"\nCall first",true);
Check(ManifestNotes.OptimizationLocked(notes)&&ManifestNotes.Read(notes)=="Call first"&&ManifestNotes.Receipt(notes)==receipt,"lock preserves notes and export receipt");
Check(ManifestNotes.OptimizationLocked(ManifestNotes.Write(notes,"Updated")),"note edits preserve lock");
var unlocked=ManifestNotes.SetPlanningLock(notes,false);
Check(!ManifestNotes.OptimizationLocked(unlocked)&&ManifestNotes.Read(unlocked)=="Call first","unlock preserves notes");
Check(ManifestNotes.SetPlanningLock(unlocked,false)!=unlocked,"lock history changes revision even after unlock");
await Reject(()=>Task.FromResult(ManifestNotes.Write(unlocked,notes)),"Use Lock");

// All native writes use a disposable embedded database, never a customer database.
var path=Path.Combine(Path.GetTempPath(),"aurora-replan-"+Guid.NewGuid().ToString("N")+".fdb");
var cs=new FbConnectionStringBuilder{Database=path,UserID="SYSDBA",Password="test",ServerType=FbServerType.Embedded,ClientLibrary=@"C:\Program Files\Firebird\Firebird_5_0\fbclient.dll",Pooling=false}.ConnectionString;
FbConnection.CreateDatabase(cs);
try
{
    await using var db=new FbConnection(cs);await db.OpenAsync();
    foreach(var ddl in new[]{
        "CREATE TABLE CT_STAT(CTS_ID BIGINT PRIMARY KEY,CTS_CODE VARCHAR(12),CTS_DESC VARCHAR(40))",
        "CREATE TABLE CT_TYPE(CTT_ID BIGINT PRIMARY KEY,CTT_CODE VARCHAR(12),CTT_DESC VARCHAR(40))",
        "CREATE TABLE LOCMAST(LM_ID VARCHAR(12) PRIMARY KEY)",
        "CREATE TABLE EQUIP_HEADER(EH_ID BIGINT PRIMARY KEY,EH_UNIT_ID VARCHAR(20),EH_UNIT_TYPE BIGINT,EH_DELETE_DATE TIMESTAMP,EH_DOMICILE VARCHAR(12),EH_MAX_WEIGHT DOUBLE PRECISION,EH_MAX_CUBE DOUBLE PRECISION)",
        "CREATE TABLE DRIVER_HEADER(DH_ID BIGINT PRIMARY KEY,DH_FIRST_NAME VARCHAR(40),DH_LAST_NAME VARCHAR(40),DH_CELL_PHONE VARCHAR(32),DH_EMAIL VARCHAR(150),DH_HIRE_DATE DATE,DH_TERM_DATE DATE,DH_DL_EXPIRY DATE,DH_ASG_TRACTOR BIGINT,DH_STD_START_TIME VARCHAR(4),DH_STD_END_TIME VARCHAR(4),DH_FULL_BURDENED_HR NUMERIC(9,4))",
        "CREATE TABLE DRIVER_PKUP_DELV(DPD_ID BIGINT,DPD_SEQUENCE INTEGER,DPD_LOAD_NUMBER BIGINT,DPD_ACTIVE_PLAN CHAR(1))",
        "CREATE TABLE OPS_HEADER(PB_ID BIGINT PRIMARY KEY,PB_TYPE CHAR(1),PB_LOAD_TYPE CHAR(1),PB_LOCATION VARCHAR(12),PBORIG VARCHAR(12),PBDEST VARCHAR(12),PB_STATUS BIGINT,PB_DT_ENT TIMESTAMP,PB_MFST_DATE TIMESTAMP,PB_CURRENT_DRIVER BIGINT,PB_CURRENT_TRACTOR BIGINT,PB_DT_CANC TIMESTAMP,PB_EXTERNAL_REF VARCHAR(128),PB_NOTES BLOB SUB_TYPE TEXT,PB_PLANNED_DRIVER BIGINT,PB_PLANNED_DRIVER2 BIGINT,PB_PLANNED_TRAILER BIGINT,PB_PLANNED_TRACTOR BIGINT,PBNUM BIGINT,PBTPIECE DECIMAL(15,2),PBTWEIGH DECIMAL(15,2),PB_TTL_CUBE DECIMAL(15,2),PBBOL VARCHAR(40))",
        "CREATE TABLE OPS_STOP_REC(OPS_STOP_ID BIGINT PRIMARY KEY,OPS_ORDER_ID BIGINT,OPS_STOP_NUMBER INTEGER,OPS_MFST_LOC_TYPE CHAR(1),OPS_MFST_LOCATION VARCHAR(12),OPS_TYPE CHAR(1),OPS_ACCTID VARCHAR(12),OPS_MANIFEST_NUMBER BIGINT,OPS_MANIFESTED_TIME TIMESTAMP,OPS_DELIVERED_TIME TIMESTAMP,OPS_DISPATCH_TIME TIMESTAMP,OPS_DEPART_TIME TIMESTAMP,OPS_BACKIN_TIME TIMESTAMP,OPS_PICKED_UP CHAR(1),OPS_STATUS BIGINT,OPS_NAME VARCHAR(80),OPS_ADDR1 VARCHAR(80),OPS_CITY VARCHAR(40),OPS_STATE VARCHAR(2),OPS_ZIP VARCHAR(12),OPS_LATITUDE DOUBLE PRECISION,OPS_LONGITUDE DOUBLE PRECISION,OPS_APPT_LOW TIMESTAMP,OPS_APPT_HIGH TIMESTAMP)",
        "CREATE TABLE OPS_MANIFEST_STOPS(OSM_STOP_ID BIGINT,OSM_SEQUENCE INTEGER,OSM_PKUP_DELV_ID BIGINT UNIQUE)",
        "CREATE TABLE OPS_HISTORY(OPH_TIME TIMESTAMP,OPH_STATUS_TIME TIMESTAMP,OPH_INITIALS VARCHAR(8),OPH_PRIOR_STATUS BIGINT,OPH_STATUS BIGINT,OPH_ORDER_ID BIGINT,OPH_STOP_ID BIGINT,OPH_REASON VARCHAR(80),OPH_SEQUENCE INTEGER)",
        "CREATE SEQUENCE ORDER_GEN START WITH 10000","CREATE SEQUENCE OPS_STOP_GEN START WITH 20000"})await db.ExecuteAsync(ddl);
    await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(1,'UNAS','Unassigned')");
    await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(2,'ASG','Assigned')");
    await db.ExecuteAsync("INSERT INTO CT_STAT VALUES(3,'DPCH','Dispatched')");
    await db.ExecuteAsync("INSERT INTO CT_TYPE VALUES(1,'TRK','Straight truck')");
    await db.ExecuteAsync("INSERT INTO LOCMAST VALUES('101')");await db.ExecuteAsync("INSERT INTO LOCMAST VALUES('102')");
    foreach(var truck in new[]{(1,"OLD"),(2,"NEW"),(3,"BUSY")})await db.ExecuteAsync("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE) VALUES(@id,@unit,1)",new{id=truck.Item1,unit=truck.Item2});
    var day=new DateOnly(2040,1,2);var date=day.ToDateTime(TimeOnly.MinValue);
    async Task Manifest(long id,int status=1,string dest="101",string? text=null,long? truck=null)=>await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_MFST_DATE,PB_NOTES,PB_CURRENT_TRACTOR) VALUES(@id,'M','M','101','101',@dest,@status,@date,@text,@truck)",new{id,status,dest,date,text,truck});
    async Task Routing(long id)=>await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION) VALUES(@stop,@id,1,'L','101')",new{stop=id*10,id});
    async Task Order(long id,long? manifest=null)
    {
        await db.ExecuteAsync("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOCATION,PB_STATUS,PBNUM,PBTPIECE,PBTWEIGH,PB_TTL_CUBE) VALUES(@id,'O','101',110,@id,1,100,10)",new{id});
        await db.ExecuteAsync("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_TYPE,OPS_MANIFEST_NUMBER,OPS_LATITUDE,OPS_LONGITUDE,OPS_NAME) VALUES(@stop,@id,2,'D',@manifest,42,-71,'House')",new{stop=id*10,id,manifest});
        if(manifest is {} m)await db.ExecuteAsync("INSERT INTO OPS_MANIFEST_STOPS VALUES(@route,1,@stop)",new{route=m*10,stop=id*10});
    }
    await Manifest(100,2,text:"Dispatcher note",truck:1);await Routing(100);await Order(10,100);
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_CURRENT_DRIVER=99 WHERE PB_ID=100");
    await db.ExecuteAsync("INSERT INTO DRIVER_PKUP_DELV VALUES(99,1,100,'A')");
    await Manifest(101,text:notes,truck:3);await Routing(101);await Order(11,101);
    await Manifest(102,3);await Routing(102);await Order(12,102);
    await Manifest(103);await Routing(103);await Order(13,103);
    await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_DEPART_TIME=CURRENT_TIMESTAMP WHERE OPS_ORDER_ID=103");
    await Manifest(104);await Routing(104);await Order(14,104);
    await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_DELIVERED_TIME=CURRENT_TIMESTAMP WHERE OPS_ORDER_ID=14");
    await Manifest(105);await Routing(105);
    await Manifest(106,dest:"102");await Routing(106);
    await Manifest(107);await Routing(107);await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_MFST_DATE=DATE '2040-01-03' WHERE PB_ID=107");
    await Order(15);
    var tenant=new TestTenant(Guid.NewGuid());await using var pg=NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("REPLAN_CHECK_PG")??"Host=localhost;Database=unused;Username=unused");
    var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(pg,tenant),tenant,Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=tenant.TenantId,ConnectionString=cs,SourceKey="check",ReadyStatus=110,TimeZoneId="America/New_York"}));
    var origin=new TerminalDto{Code="101",Latitude=42,Longitude=-71};
    var input="""{"orders":{"deliveries":[{"id":"FB-15","delivery":{"locationId":"FB-15"}}]},"locations":[{"id":"DEPOT","latitude":42,"longitude":-71},{"id":"FB-15","latitude":42,"longitude":-71}],"reporting":{"terminal":"101","firebirdSource":"check","orders":{"FB-15":{"nativeOrderId":15,"nativeStopId":150,"nativeStopKind":"D"}}},"vehicles":[{"id":"NEW","start":{"earliestStartTime":"2040-01-02T06:00:00-05:00"}}]}""";
    var plan=await source.IncludeOpenManifests(input,origin,null,day,default);
    Check(plan.Manifests.Select(m=>m.Id).SequenceEqual(new long[]{100,105}),"includes assigned and empty compatible manifests; excludes locked, dispatched, started, delivered, other destination and other date");
    Check(OrderWorkspace.OrderIds(plan.Request).ToHashSet().SetEquals(new[]{"FB-10","FB-15"}),"existing and selected orders merged");
    Check(await db.ExecuteScalarAsync<long>("SELECT OPS_MANIFEST_NUMBER FROM OPS_STOP_REC WHERE OPS_STOP_ID=100")==100,"planning leaves native membership untouched");
    RouteSummaryDto Route(string vehicle,params string[] ids)=>new(vehicle,ids.Length,ids.Length,10,100,20,0.1,0.1,"101",36000,100,ids.Length,ids.Length,ids.Length*100,ids.Length*10,ids.Select((id,i)=>new ManifestProDto(id,i+1,"House","","","","",1,100,10,"",60,42,-71)).ToArray(),[],new DateTimeOffset(2040,1,2,6,0,0,TimeSpan.FromHours(-5)));
    var routes=new[]{Route("NEW","FB-15","FB-10")};
    ManifestReplanning.ValidateResult(plan.Request,routes);Check(true,"orders can change trucks and sequence");
    await Reject(()=>Task.Run(()=>ManifestReplanning.ValidateResult(plan.Request,[Route("NEW","FB-15")])),"must remain scheduled");
    var tampered=RoutingInput.Parse(plan.Request);tampered["reporting"]!.AsObject().Remove("replanManifests");
    Check(ManifestReplanning.Included(ManifestReplanning.Protect(plan.Request,tampered.ToJsonString())).Length==2,"client cannot remove protected manifest list");
    tampered["vehicles"]![0]!["start"]!["earliestStartTime"]="2040-01-03T06:00:00-05:00";
    await Reject(()=>Task.Run(()=>ManifestReplanning.Protect(plan.Request,tampered.ToJsonString())),"Keep the planning date");
    var fleet=RoutingInput.Parse(plan.Request);fleet["vehicles"]!.AsArray().Add(new JsonObject{["id"]="BUSY"});
    Check(!RoutingInput.Items(RoutingInput.Parse(await source.ExcludeBookedTrucks(fleet.ToJsonString(),day,default))["vehicles"]).Any(v=>RoutingInput.Text(v["id"])=="BUSY"),"locked manifest truck excluded from fleet");
    var draft=Guid.NewGuid();var session=Guid.NewGuid();
    async Task<Dictionary<string,long>> Export(string json,RouteSummaryDto[] result,bool rollback=false)
    {
        await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait});
        return await source.ExportCore(db,tx,draft,session,json,result,[110],[],default,rollback);
    }
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_NOTES=@text WHERE PB_ID=100",new{text=ManifestNotes.SetPlanningLock("Dispatcher note",true)});
    await Reject(()=>Export(plan.Request,routes),"changed or was locked/dispatched");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=1000")==1,"conflict saves nothing");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_NOTES='Dispatcher note' WHERE PB_ID=100");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTWEIGH=200 WHERE PB_ID=10");
    await Reject(()=>Export(plan.Request,routes),"changed or was locked/dispatched");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTWEIGH=100 WHERE PB_ID=10");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_STATUS=3 WHERE PB_ID=100");
    await Reject(()=>Export(plan.Request,routes),"changed or was locked/dispatched");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_STATUS=2 WHERE PB_ID=100");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_STATUS=999 WHERE PB_ID=15");
    await Reject(()=>Export(plan.Request,routes),"changed or is already manifested");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=100")==1&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=1000")==1,"failure after replacement starts rolls back assignments and membership");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_STATUS=110 WHERE PB_ID=15");
    await Reject(()=>Export(plan.Request,[Route("BUSY","FB-10","FB-15")]),"reserved by another manifest");
    await Export(plan.Request,routes,true);
    Check(await db.ExecuteScalarAsync<long>("SELECT PB_CURRENT_TRACTOR FROM OPS_HEADER WHERE PB_ID=100")==1&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=100")==1,"rollback restores original truck and driver");
    Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=1000")==1,"rollback restores original membership");
    var saved=await Export(plan.Request,routes);
    Check(saved["NEW"]==100,"save reuses manifest number with new truck");
    Check(await db.ExecuteScalarAsync<long>("SELECT PB_PLANNED_TRAILER FROM OPS_HEADER WHERE PB_ID=100")==2,"new optimized truck saved");
    Check(await db.ExecuteScalarAsync<long?>("SELECT PB_CURRENT_TRACTOR FROM OPS_HEADER WHERE PB_ID=100")is null&&await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER=100")==0,"old truck and driver assignments released");
    Check((await db.QueryAsync<long>("SELECT OSM_PKUP_DELV_ID FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=1000 ORDER BY OSM_SEQUENCE")).SequenceEqual(new long[]{150,100}),"existing and new orders saved in optimized sequence");
    Check(await db.ExecuteScalarAsync<string>("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=101")==notes&&await db.ExecuteScalarAsync<long>("SELECT OPS_MANIFEST_NUMBER FROM OPS_STOP_REC WHERE OPS_STOP_ID=110")==101,"locked manifest and orders untouched");
    Check(ManifestNotes.Read(await db.ExecuteScalarAsync<string>("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=100"))=="Dispatcher note","reused manifest retains dispatcher notes");
    Check((await Export(plan.Request,routes))["NEW"]==100,"lost response retry reuses committed export");
    await Order(16);var more=RoutingInput.Parse(input);more["orders"]!["deliveries"]![0]!["id"]="FB-16";more["orders"]!["deliveries"]![0]!["delivery"]!["locationId"]="FB-16";more["locations"]![1]!["id"]="FB-16";more["reporting"]!["orders"]=new JsonObject{["FB-16"]=new JsonObject{["nativeOrderId"]=16,["nativeStopId"]=160,["nativeStopKind"]="D"}};
    var next=await source.IncludeOpenManifests(more.ToJsonString(),origin,null,day,default);draft=Guid.NewGuid();session=Guid.NewGuid();
    var split=await Export(next.Request,[Route("NEW","FB-10"),Route("OLD","FB-15","FB-16")]);
    Check(split.Values.ToHashSet().SetEquals(new long[]{100,105}),"split result fills existing empty manifest");
    if(Environment.GetEnvironmentVariable("REPLAN_CHECK_PG") is not null)
    {
        await using var sql=await pg.OpenConnectionAsync();
        if(!sql.Database.StartsWith("aurora_replan_"))throw new Exception("Only disposable replanning databases allowed.");
        await sql.ExecuteAsync("INSERT INTO tenant(id,name,slug) VALUES(@id,'Replanning check',@slug)",new{id=tenant.TenantId,slug=tenant.TenantId.ToString()});
        await Order(17);
        var added=RoutingInput.Parse(input);added["orders"]!["deliveries"]![0]!["id"]="FB-17";added["orders"]!["deliveries"]![0]!["delivery"]!["locationId"]="FB-17";added["locations"]![1]!["id"]="FB-17";added["reporting"]!["orders"]=new JsonObject{["FB-17"]=new JsonObject{["nativeOrderId"]=17,["nativeStopId"]=170,["nativeStopKind"]="D"}};
        var combined=await source.IncludeOpenManifests(added.ToJsonString(),origin,null,day,default);
        var sourceId=Guid.NewGuid();var draftId=Guid.NewGuid();var oldDraft=Guid.NewGuid();var localManifest=Guid.NewGuid();var sessionId=Guid.NewGuid();
        await sql.ExecuteAsync("INSERT INTO aurora_order_source(tenant_id,id,name,request) VALUES(@tenant,@sourceId,'Fixture',CAST(@json AS jsonb))",new{tenant=tenant.TenantId,sourceId,json=combined.Request});
        foreach(var pair in new[]{(oldDraft,true),(draftId,false)})
            await sql.ExecuteAsync("INSERT INTO aurora_planning_draft(tenant_id,id,owner_id,source_id,order_ids,request,finished_session,name) VALUES(@tenant,@id,'owner',@sourceId,@ids,CAST(@json AS jsonb),@finished,'Fixture')",new{tenant=tenant.TenantId,id=pair.Item1,sourceId,ids=OrderWorkspace.OrderIds(combined.Request),json=combined.Request,finished=pair.Item2?(Guid?)Guid.NewGuid():null});
        foreach(var orderId in new[]{"FB-10","FB-17"})
            await sql.ExecuteAsync("INSERT INTO aurora_order(tenant_id,source_id,id,scheduled_at,customer,city,status) VALUES(@tenant,@sourceId,@orderId,now(),'House','Boston',@status)",new{tenant=tenant.TenantId,sourceId,orderId,status=orderId=="FB-17"?WorkspaceOrderStatus.Ready:WorkspaceOrderStatus.Routed});
        await sql.ExecuteAsync("INSERT INTO aurora_manifest(tenant_id,id,draft_id,vehicle,data,number) VALUES(@tenant,@localManifest,@oldDraft,'NEW',CAST(@data AS jsonb),'100')",new{tenant=tenant.TenantId,localManifest,oldDraft,data=JsonSerializer.Serialize(Route("NEW","FB-10"))});
        await sql.ExecuteAsync("INSERT INTO aurora_manifest_order VALUES(@tenant,@localManifest,@sourceId,'FB-10',1)",new{tenant=tenant.TenantId,localManifest,sourceId});
        var mergedRoute=Route("OLD","FB-17","FB-15","FB-10","FB-16");
        var summary=new RunSummaryDto("SUCCEEDED",1,2,4,0,10,100,20,0,4,5,5,2.5,4,"Weight");
        var result=new OptimizationResultDto("Fixture",null,DateTimeOffset.UtcNow,"101","SUCCEEDED",200,"OK",1,false,summary,[mergedRoute],[],[],[],[],[],"{}",null,null);
        var workspace=new OrderWorkspace(new NpgsqlConnectionFactory(pg,tenant),tenant,source);
        await sql.ExecuteAsync("CREATE OR REPLACE FUNCTION replan_fail_save() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Simulated PostgreSQL failure'; END $$; CREATE TRIGGER replan_fail_save BEFORE UPDATE ON aurora_manifest FOR EACH ROW EXECUTE FUNCTION replan_fail_save()");
        try { await workspace.Finish(draftId,sessionId,"owner",result,default);throw new Exception("Expected PostgreSQL failure"); }
        catch(PostgresException e) when(e.MessageText=="Simulated PostgreSQL failure") { Check(true,"simulated PostgreSQL failure after native commit"); }
        finally { await sql.ExecuteAsync("DROP TRIGGER replan_fail_save ON aurora_manifest; DROP FUNCTION replan_fail_save()"); }
        Check(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_manifest_order WHERE tenant_id=@id",new{id=tenant.TenantId})==1,"failed PostgreSQL transaction retains original local membership");
        var complete=await workspace.Finish(draftId,sessionId,"owner",result,default);
        Check(complete.Count==1&&complete[0].Id==localManifest&&complete[0].Number=="100","PostgreSQL save reuses existing local manifest identity");
        Check(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_manifest_order WHERE tenant_id=@id",new{id=tenant.TenantId})==4,"PostgreSQL replaces membership and imports native-only orders");
        Check(await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM aurora_order WHERE tenant_id=@id AND status='Routed'",new{id=tenant.TenantId})==4,"all old and new orders remain Routed");
        Check((await workspace.Finish(draftId,sessionId,"owner",result,default)).Single().Id==localManifest,"completed workspace save is idempotent");
        var policy=new CustomerPlanning{Consolidation=true,EquipmentType="NOVA-1",MaximumWeight=200,MaximumCubes=20,EstimatedCubesPerPiece=10};
        async Task<FirebirdRoutingSource.SourceOrder> ConsolidationOrder(long id,string customer="STORE",string address="1 Main St",decimal weight=100,decimal cubes=10)
        {
            await Order(id);
            await db.ExecuteAsync("UPDATE OPS_HEADER SET PBTWEIGH=@weight,PB_TTL_CUBE=@cubes WHERE PB_ID=@id",new{id,weight,cubes});
            await db.ExecuteAsync("UPDATE OPS_STOP_REC SET OPS_ACCTID=@customer,OPS_ADDR1=@address,OPS_CITY='Boston',OPS_STATE='MA',OPS_ZIP='02101' WHERE OPS_ORDER_ID=@id",new{id,customer,address});
            return new(){NativeId=id,StatusId=110,Pro=id,StopId=id*10,StopKind="D",CustomerCode=customer,Address=address,City="Boston",State="MA",PostalCode="02101",Terminal="101",Weight=weight,Cube=cubes,Pieces=1,Policy=policy};
        }
        async Task<string[]> Accumulate(params FirebirdRoutingSource.SourceOrder[] selected)
        {
            await using var t=await sql.BeginTransactionAsync();var warnings=new List<string>();
            await source.AccumulateConsolidation(sql,t,selected,warnings,default);
            await t.CommitAsync();return warnings.ToArray();
        }
        async Task<long> ManifestOf(long id)=>await db.ExecuteScalarAsync<long>("SELECT COALESCE(OPS_MANIFEST_NUMBER,0) FROM OPS_STOP_REC WHERE OPS_ORDER_ID=@id",new{id});
        var a=await ConsolidationOrder(20);var b=await ConsolidationOrder(21);var c=await ConsolidationOrder(22);
        Check((await Accumulate(a,b,c)).Length==0,"automatic consolidation accepts valid customer freight");
        var full=await ManifestOf(20);var partial=await ManifestOf(22);
        Check(full==await ManifestOf(21)&&full!=partial,"full customer manifest overflows into a new manifest");
        var fullNotes=await db.ExecuteScalarAsync<string>("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@full",new{full});
        Check(ManifestNotes.Consolidation(fullNotes)is{Held:true,Customer:"STORE"}&&ManifestNotes.OptimizationLocked(fullNotes),"customer loads are held and excluded from optimization");
        await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_MFST_DATE=DATE '2039-12-20' WHERE PB_ID=@partial",new{partial});
        var estimated=await ConsolidationOrder(23,cubes:0);
        await Accumulate(estimated);
        Check(await ManifestOf(23)==partial,"consolidation fills an existing manifest across dates");
        var partialNotes=await db.ExecuteScalarAsync<string>("SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID=@partial",new{partial});
        Check(ManifestNotes.Consolidation(partialNotes)!.EstimatedCubes[230]==10,"estimated cubes recorded without replacing actual order data");
        Check(await db.ExecuteScalarAsync<decimal>("SELECT PB_TTL_CUBE FROM OPS_HEADER WHERE PB_ID=23")==0,"native actual cubes preserved");
        Check(ManifestNotes.Consolidation(ManifestNotes.Write(partialNotes,"Call store"))!.Customer=="STORE","dispatcher note edits preserve consolidation settings");
        NovaDispatchDocument held;
        await using(var nativeTx=db.BeginTransaction()) {held=await source.DispatchDocument(db,nativeTx,partial,false,default);await nativeTx.RollbackAsync();}
        Check(held.Consolidation is {Held:true,Full:true,EstimatedOrders:1}&&held.Issues.Any(i=>i.Contains("Release this")),"held manifest displays capacity and cannot dispatch");
        await Reject(()=>source.ReleaseConsolidation(partial,new("stale"),default),"changed");
        var released=await source.ReleaseConsolidation(partial,new(held.Revision),default);
        Check(released.Consolidation is {Held:false}&&!released.Issues.Any(i=>i.Contains("Release this")),"release permits dispatch without allowing further automatic filling");
        Check((await source.ReleaseConsolidation(partial,new(held.Revision),default)).Consolidation is {Held:false},"release retry is idempotent");
        var differentAddress=await ConsolidationOrder(24,address:"2 Main St");var differentCustomer=await ConsolidationOrder(25,customer:"OTHER");
        await Accumulate(differentAddress,differentCustomer);
        Check(await ManifestOf(24)!=await ManifestOf(25)&&await ManifestOf(24)!=partial,"customers and delivery addresses never mix");
        var oversized=await ConsolidationOrder(26,weight:300);var unknown=await ConsolidationOrder(27,cubes:0);unknown.Policy=new(){Consolidation=true,EquipmentType="NOVA-1",MaximumWeight=200,MaximumCubes=20};
        Check((await Accumulate(oversized,unknown)).Length==2&&await ManifestOf(26)==0&&await ManifestOf(27)==0,"oversized and unknown-capacity orders wait for correction");
        // Repeat refresh only processes still-unmanifested rows, matching Synchronize.
        await Accumulate(oversized,unknown);
        Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OPS_MANIFEST_STOPS WHERE OSM_PKUP_DELV_ID=230")==1,"repeated refresh cannot duplicate existing consolidation membership");
        await using(var nativeTx=db.BeginTransaction())
        {
            Check(await source.Snapshot(db,nativeTx,partial,false,default)is null,"customer consolidation excluded from ordinary reoptimization");
            await nativeTx.RollbackAsync();
        }
    }
    await Manifest(108,text:ManifestNotes.WithTripUntil("Trip",day.AddDays(2)),truck:2);await Routing(108);
    await using(var bookingTx=db.BeginTransaction())
    {
        Check((await FirebirdRoutingSource.BookedTrucks(db,bookingTx,[],day.AddDays(1),default)).Contains("NEW"),"multi-day truck stays reserved on an intermediate day");
        Check(!(await FirebirdRoutingSource.BookedTrucks(db,bookingTx,[],day.AddDays(3),default)).Contains("NEW"),"multi-day reservation ends after return date");
        await bookingTx.RollbackAsync();
    }
    await Order(18);
    await db.ExecuteAsync("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE) VALUES(4,'FREE',1)");
    draft=Guid.NewGuid();session=Guid.NewGuid();
    var newPlan=input.Replace("FB-15","FB-18").Replace("\"nativeOrderId\":15","\"nativeOrderId\":18").Replace("\"nativeStopId\":150","\"nativeStopId\":180");
    await db.ExecuteAsync("INSERT INTO CT_TYPE VALUES(2,'DRV','Driver')");
    await db.ExecuteAsync("UPDATE EQUIP_HEADER SET EH_DOMICILE='101' WHERE EH_ID=4");
    await db.ExecuteAsync("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE,EH_DOMICILE) VALUES(99,'DRIVER1',2,'101')");
    await db.ExecuteAsync("INSERT INTO DRIVER_HEADER(DH_ID,DH_FIRST_NAME,DH_LAST_NAME,DH_ASG_TRACTOR,DH_STD_START_TIME,DH_STD_END_TIME,DH_FULL_BURDENED_HR) VALUES(99,'Pat','Driver',NULL,'0800','1200',30)");
    var nativeDriver=(await source.ReadDrivers(default)).Drivers.Single();
    var withDriver=RoutingInput.Parse(newPlan);withDriver["reporting"]!["driverCostBasis"]=DriverCostBasis.Separate;
    withDriver["reporting"]!["drivers"]=new JsonObject{["FREE"]=new JsonObject{["id"]="99",["revision"]=nativeDriver.Revision,["shiftStart"]="06:00",["shiftEnd"]="18:00"}};
    newPlan=withDriver.ToJsonString();
    var drivenRoute=Route("FREE","FB-18") with{Return=new DateTimeOffset(2040,1,2,16,0,0,TimeSpan.FromHours(-5))};
    await db.ExecuteAsync("UPDATE DRIVER_HEADER SET DH_FULL_BURDENED_HR=31 WHERE DH_ID=99");
    await Reject(()=>Export(newPlan,[drivenRoute]),"driver for truck FREE changed");
    await db.ExecuteAsync("UPDATE DRIVER_HEADER SET DH_FULL_BURDENED_HR=30 WHERE DH_ID=99");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_PLANNED_DRIVER=99 WHERE PB_ID=108");
    await Reject(()=>Export(newPlan,[drivenRoute]),"Driver DRIVER1 was reserved");
    await db.ExecuteAsync("UPDATE OPS_HEADER SET PB_PLANNED_DRIVER=NULL WHERE PB_ID=108");
    var created=await Export(newPlan,[drivenRoute]);
    Check(await db.ExecuteScalarAsync<long>("SELECT PB_PLANNED_DRIVER FROM OPS_HEADER WHERE PB_ID=@id",new{id=created["FREE"]})==99,"export uses selected driver and per-plan hours despite different profile defaults");
    await using(var dispatchTx=db.BeginTransaction())
    {
        var dispatch=await source.DispatchDocument(db,dispatchTx,created["FREE"],false,default);
        Check(dispatch.PlannedDriverId==99&&dispatch.Drivers.Single().Name.Contains("Pat Driver"),"dispatch shows planned driver and native name");
        await dispatchTx.RollbackAsync();
    }
    Check((await Export(newPlan,[drivenRoute]))["FREE"]==created["FREE"],"driver export retry is idempotent");
    Check(created["FREE"]>=10000&&await db.ExecuteScalarAsync<long>("SELECT OPS_MANIFEST_NUMBER FROM OPS_STOP_REC WHERE OPS_STOP_ID=180")==created["FREE"],"creates new manifest when no compatible manifest was included");
    Console.WriteLine($"{checks} manifest replanning checks passed.");
}
finally{FbConnection.DropDatabase(cs);}
sealed class TestTenant(Guid id):ITenantContext{public Guid TenantId=>id;public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}
