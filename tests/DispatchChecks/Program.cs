using System.Security.Claims;
using System.Text.Json;
using Aurora.Api.Features.Auth;
using Aurora.Api.Features.Routing;
using Aurora.Contracts;
using Aurora.Infrastructure.Data;
using Aurora.Infrastructure.Tenancy;
using Aurora.Modules.Routing;
using Dapper;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
async Task Reject(Func<Task> action,string fragment){try{await action();}catch(FormatException e) when(e.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){Check(true,"Rejects "+fragment);return;}throw new Exception("Did not reject "+fragment);}
var receipt="Aurora routing export v1 "+new string('A',64);
Check(ManifestNotes.Read(receipt)=="","export receipt is hidden from dispatcher notes");
Check(ManifestNotes.Read(ManifestNotes.Write(receipt,"Call before departure"))=="Call before departure","notes round-trip beside the export receipt");
Check(ManifestNotes.Receipt(ManifestNotes.Write(receipt,"Call before departure"))==receipt,"notes preserve export retry receipt");
Check(ManifestNotes.Write(receipt," ").StartsWith(receipt),"clearing notes preserves receipt");
Check(ManifestNotes.Read("Existing Nova instructions")=="Existing Nova instructions","ordinary Nova notes remain visible");
await Reject(()=>Task.FromResult(ManifestNotes.Write(receipt,new string('x',4001))),"4,000");
var tenantId=Guid.NewGuid(); var draft=Guid.NewGuid();
var reference=FirebirdRoutingSource.ExportKey(tenantId,draft,"TEST-TRUCK");
Check(FirebirdRoutingSource.PlannedDispatchTruck(reference,[(1,"WRONG"),(2,"TEST-TRUCK")],tenantId)?.Id==2,"accepted plan truck is recovered without changing export");
Check(FirebirdRoutingSource.PlannedDispatchTruck(reference,[(2,"TEST-TRUCK")],Guid.NewGuid()) is null,"planned vehicle identity is tenant scoped");
Check(FirebirdRoutingSource.PlannedDispatchTruck(reference,[(1,"WRONG")],tenantId) is null,"missing optimized truck cannot be substituted");
Check(AppLog.Describe("/api/v1/aurora/firebird/manifests/{id}/dispatch","POST",200)=="Dispatched manifest","plain dispatch activity");
Check(AppLog.Describe("/api/v1/aurora/firebird/manifests/{id}/assignment","PUT",200)=="Assigned manifest","plain assignment activity");
var builder=WebApplication.CreateBuilder();builder.Services.AddAuthorization();builder.Services.AddScoped<FirebirdRoutingSource>();builder.Services.AddScoped<OrderWorkspace>();builder.Services.AddScoped<PlanningSessions>();
await using var app=builder.Build();app.MapOrderWorkspaceEndpoints();
var policies=app.Services.GetRequiredService<IAuthorizationPolicyProvider>();var authorization=app.Services.GetRequiredService<IAuthorizationService>();
var endpoints=((IEndpointRouteBuilder)app).DataSources.SelectMany(s=>s.Endpoints).OfType<RouteEndpoint>().Where(e=>e.RoutePattern.RawText!.EndsWith("/dispatch")||e.RoutePattern.RawText!.EndsWith("/assignment")||e.RoutePattern.RawText!.EndsWith("/status")||e.RoutePattern.RawText!.EndsWith("/notes")).ToArray();
Check(endpoints.Length==5,"manifest assignment, dispatch and status endpoints available");
foreach(var endpoint in endpoints){var policy=(await AuthorizationPolicy.CombineAsync(policies,endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()))!;Check(!(await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()),null,policy)).Succeeded,"anonymous dispatch access denied");}
if(Environment.GetEnvironmentVariable("FIREBIRD_CHECK_CONFIG") is {} path)
{
    var config=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(path))!.ToDictionary(x=>x.Key,x=>x.Value.ToString());
    var cs=new FbConnectionStringBuilder(config["FirebirdRouting:ConnectionString"]);
    if(cs.DataSource!="172.23.0.5"||!cs.Database.Equals(@"D:\db\wayl_test\operations.fdb",StringComparison.OrdinalIgnoreCase))throw new Exception("Only WayTest is allowed.");
    var tenant=new TestTenant(Guid.Parse(config["FirebirdRouting:TenantId"]));
    await using var pg=NpgsqlDataSource.Create(config["ConnectionStrings:Aurora"]);
    var source=new FirebirdRoutingSource(new NpgsqlConnectionFactory(pg,tenant),tenant,Options.Create(new FirebirdRoutingOptions{Enabled=true,TenantId=tenant.TenantId,ConnectionString=cs.ConnectionString,SourceKey="dispatch-check"}));
    await using var db=new FbConnection(cs.ConnectionString);await db.OpenAsync();
    await using var tx=db.BeginTransaction(new FbTransactionOptions{TransactionBehavior=FbTransactionBehavior.Write|FbTransactionBehavior.ReadCommitted|FbTransactionBehavior.NoRecVersion|FbTransactionBehavior.Wait});
    // Every fixture, trigger side effect and tested write stays in this one rolled-back transaction.
    const long m=1990990001, order=1990990002, route=1990990003, stop=1990990004, other=1990990009;
    // Equipment IDs must be positive for the public request validation.
    const long d=1990990005,t=1990990006,d2=1990990007,t2=1990990008;
    var day=new DateTime(2040,1,2);
    async Task Exec(string sql,object? args=null)=>_ = await db.ExecuteAsync(sql,args,tx);
    async Task<T> Scalar<T>(string sql)=>(await db.ExecuteScalarAsync<T>(sql,transaction:tx))!;
    async Task<NovaDispatchDocument> Read(long id=m)=>await source.DispatchDocument(db,tx,id,false,default);
    async Task<NovaDispatchDocument> Write(NovaDispatchDocument doc,long drv=d,long trk=t,bool release=false)=>await source.WriteDispatch(db,tx,doc.ManifestId,new(doc.Revision,drv,trk),release,"CHECK",default);
    try
    {
        foreach(var e in new[]{(d,"ZX-DISP-D1","DRV"),(t,"ZX-DISP-T1","TRK"),(d2,"ZX-DISP-D2","DRV"),(t2,"ZX-DISP-T2","TRK")})
            await Exec("INSERT INTO EQUIP_HEADER(EH_ID,EH_UNIT_ID,EH_UNIT_TYPE,EH_DOMICILE,EH_MAX_WEIGHT,EH_MAX_CUBE) SELECT @id,@name,CTT_ID,'MHT',20000,2000 FROM CT_TYPE WHERE CTT_CODE=@kind",new{id=e.Item1,name=e.Item2,kind=e.Item3});
        await Exec("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE) VALUES(@m,'M','M','MHT','MHT','MHT',1,@day,@day)",new{m,day});
        await Exec("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PB_STATUS,PB_DT_ENT,PBTWEIGH,PB_TTL_CUBE) VALUES(@order,'O','O','MHT',110,@day,100,10)",new{order,day});
        await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_MFST_LOC_TYPE,OPS_MFST_LOCATION) VALUES(@route,@m,1,'L','MHT')",new{route,m});
        await Exec("INSERT INTO OPS_STOP_REC(OPS_STOP_ID,OPS_ORDER_ID,OPS_STOP_NUMBER,OPS_TYPE,OPS_MANIFEST_NUMBER) VALUES(@stop,@order,2,'D',@m)",new{stop,order,m});
        await Exec("INSERT INTO OPS_MANIFEST_STOPS(OSM_STOP_ID,OSM_SEQUENCE,OSM_PKUP_DELV_ID) VALUES(@route,1,@stop)",new{route,stop});
        var initial=await Read();Check(initial.CanEdit&&initial.Issues.Length==0,"unassigned populated manifest is dispatchable");
        await Exec("UPDATE OPS_HEADER SET PB_NOTES=@receipt WHERE PB_ID=@m",new{m,receipt});
        var beforeNotes=await Read();
        var noted=await source.WriteManifestNotes(db,tx,m,new(beforeNotes.Revision,"Leave at receiving dock."),"CHECK",default);
        Check(noted.Notes=="Leave at receiving dock." && noted.StatusCode==beforeNotes.StatusCode && noted.DriverId==beforeNotes.DriverId,"notes save without assignment or status change");
        Check(ManifestNotes.Receipt(await Scalar<string>($"SELECT PB_NOTES FROM OPS_HEADER WHERE PB_ID={m}"))==receipt,"native notes save retains receipt");
        Check((await Read()).Notes==noted.Notes,"native notes persist on reload");
        await Reject(()=>source.WriteManifestNotes(db,tx,m,new(beforeNotes.Revision,"Stale edit"),"CHECK",default),"changed");
        await Reject(()=>source.WriteManifestNotes(db,tx,m,new(noted.Revision,new string('x',4001)),"CHECK",default),"4,000");
        await Exec("UPDATE OPS_HEADER SET PB_DT_CANC=@day WHERE PB_ID=@m",new{m,day});
        await Reject(async()=>await source.WriteManifestNotes(db,tx,m,new((await Read()).Revision,"Blocked"),"CHECK",default),"closed or canceled");
        await Exec("UPDATE OPS_HEADER SET PB_DT_CANC=NULL WHERE PB_ID=@m",new{m});
        await Reject(()=>source.WriteDispatch(db,tx,m,new("stale",d,t),false,"CHECK",default),"changed");
        initial=await Read();
        await Reject(()=>Write(initial,t,d),"active driver");
        await Exec("UPDATE EQUIP_HEADER SET EH_DOMICILE='PWM' WHERE EH_ID=@t",new{t});
        await Reject(async ()=>await Write(await Read()),"another terminal");
        await Exec("UPDATE EQUIP_HEADER SET EH_DOMICILE='MHT',EH_MAX_WEIGHT=1 WHERE EH_ID=@t",new{t});
        await Reject(async ()=>await Write(await Read()),"capacity");
        await Exec("UPDATE EQUIP_HEADER SET EH_MAX_WEIGHT=20000 WHERE EH_ID=@t",new{t});
        await Exec("UPDATE OPS_HEADER SET PB_EXTERNAL_REF=@reference WHERE PB_ID=@m",new{m,reference=FirebirdRoutingSource.ExportKey(tenant.TenantId,Guid.NewGuid(),"ZX-DISP-T1")});
        var planned=await Read();Check(planned.PlannedTruckId==t,"database manifest resolves exact optimized truck");
        await Reject(()=>Write(planned,d,t2),"accepted optimization");
        var assigned=await Write(planned);Check(assigned.Status=="Assigned"&&assigned.DriverId==d&&assigned.TruckId==t,"saves native driver and truck assignment");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM DRIVER_PKUP_DELV WHERE DPD_ID={d} AND DPD_LOAD_NUMBER={m}")==1,"one native driver assignment link");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_ORDER_ID={order} AND OPS_DISPATCH_TIME IS NOT NULL")==0,"saving assignment does not dispatch orders");
        var reassigned=await Write(assigned,d2);Check(reassigned.DriverId==d2,"driver can change before dispatch");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER={m}")==1,"reassignment replaces link without duplicates");
        await Exec("INSERT INTO OPS_HEADER(PB_ID,PB_TYPE,PB_LOAD_TYPE,PB_LOCATION,PBORIG,PBDEST,PB_STATUS,PB_DT_ENT,PB_MFST_DATE,PB_CURRENT_DRIVER) VALUES(@other,'M','M','MHT','MHT','MHT',18,@day,@day,@d)",new{other,day,d});
        await Reject(async ()=>await Write(await Read(),d),"Already assigned");
        await Exec("UPDATE OPS_HEADER SET PB_CURRENT_DRIVER=NULL WHERE PB_ID=@other",new{other});
        await Exec("UPDATE OPS_HEADER SET PB_DT_CANC=@day WHERE PB_ID=@order",new{order,day});
        await Reject(async ()=>await Write(await Read(),d2),"canceled");
        await Exec("UPDATE OPS_HEADER SET PB_DT_CANC=NULL WHERE PB_ID=@order",new{order});
        var before=await Scalar<string>($"SELECT LIST(CAST(OSM_SEQUENCE AS VARCHAR(10))||':'||CAST(OSM_PKUP_DELV_ID AS VARCHAR(20))) FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID={route}");
        await Exec("SAVEPOINT before_release");
        var dispatched=await Write(await Read(),d2,t,true);Check(dispatched.Dispatched&&!dispatched.CanEdit&&dispatched.DispatchedAt is not null,"release updates native status and locks assignment");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_HEADER h JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS WHERE h.PB_ID={order} AND s.CTS_CODE='DPCH'")==1,"delivery order reflects dispatch, including null previous stop status");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_STOP_ID={stop} AND OPS_DRIVER1={d2} AND OPS_TRACTOR={t} AND OPS_DISPATCH_TIME IS NOT NULL AND OPS_DELIVERED_TIME IS NULL AND OPS_DEPART_TIME IS NULL")==1,"dispatch stamps equipment without claiming departure or delivery");
        Check(before==await Scalar<string>($"SELECT LIST(CAST(OSM_SEQUENCE AS VARCHAR(10))||':'||CAST(OSM_PKUP_DELV_ID AS VARCHAR(20))) FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID={route}"),"optimized route membership and sequence unchanged");
        var history=await Scalar<int>($"SELECT COUNT(*) FROM OPS_HISTORY WHERE OPH_ORDER_ID={m}");
        var retry=await Write(reassigned,d2,t,true);Check(retry.Revision==dispatched.Revision&&await Scalar<int>($"SELECT COUNT(*) FROM OPS_HISTORY WHERE OPH_ORDER_ID={m}")==history,"duplicate release is idempotent even with old revision");
        await Reject(()=>Write(dispatched,d),"already started");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM EQUIP_HISTORY WHERE EH_ID IN ({d2},{t}) AND EH_MANIFEST_NUMBER={m}")>=2,"native equipment dispatch history written");
        var departAt=dispatched.DispatchedAt!.Value.AddSeconds(1);
        async Task<NovaDispatchDocument> Status(NovaDispatchDocument doc,string code,DateTime? time)=>await source.WriteManifestStatus(db,tx,m,new(doc.Revision,code,d2,t,time),"CHECK",default);
        Check(dispatched.AllowedStatuses.SequenceEqual(new[]{"ENR"}),"dispatched manifest offers departure only");
        await Reject(()=>Status(dispatched,"CPLT",departAt),"transition");
        await Reject(()=>Status(dispatched,"ENR",null),"actual event");
        await Reject(()=>Status(dispatched,"ENR",departAt.AddDays(1)),"future");
        await Reject(()=>Status(dispatched,"ENR",departAt.AddMinutes(-1)),"previous event");
        await Reject(()=>Status(dispatched with{Revision="stale"},"ENR",departAt),"changed");
        var transit=await Status(dispatched,"ENR",departAt);
        Check(transit.StatusCode=="ENR" && transit.DepartedAt==departAt,"departure changes status and saves actual time");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_STOP_ID={stop} AND OPS_DELIVERED_TIME IS NULL")==1,"departure never marks orders delivered");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_HEADER h JOIN CT_STAT s ON s.CTS_ID=h.PB_STATUS WHERE h.PB_ID={order} AND s.CTS_CODE='ENR'")==1,"local order status follows departure");
        var departedHistory=await Scalar<int>($"SELECT COUNT(*) FROM OPS_HISTORY WHERE OPH_ORDER_ID={m}");
        await Status(dispatched,"ENR",departAt);
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_HISTORY WHERE OPH_ORDER_ID={m}")==departedHistory,"departure retry does not duplicate history");
        await Reject(()=>Status(transit,"CPLT",departAt.AddSeconds(2)),"delivery");
        await Exec("UPDATE OPS_STOP_REC SET OPS_DELIVERED_TIME=@time WHERE OPS_STOP_ID=@stop",new{time=departAt.AddSeconds(1),stop});
        await Reject(()=>Status(transit,"CPLT",departAt.AddSeconds(2)),"changed");
        var readyToClose=await Read();
        await Reject(()=>Status(readyToClose,"CPLT",departAt),"precede");
        var closed=await Status(readyToClose,"CPLT",departAt.AddSeconds(2));
        Check(closed.StatusCode=="CPLT" && closed.ClosedAt==departAt.AddSeconds(2) && closed.AllowedStatuses.Length==0,"reconciled manifest can close with actual time");
        Check(closed.History.Any(h=>h.Operator=="CHECK" && h.OccurredAt==departAt.AddSeconds(2) && h.Status=="Complete"),"status history shows operator and effective time");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM DRIVER_PKUP_DELV WHERE DPD_LOAD_NUMBER={m}")==0,"close releases driver booking");
        await Reject(()=>Status(closed,"ENR",departAt),"transition");
        await Exec("ROLLBACK TO SAVEPOINT before_release");
        await Exec("UPDATE OPS_HEADER SET PBDEST='PWM' WHERE PB_ID=@m",new{m});
        await Write(await Read(),d2,t,true);
        Check(await Scalar<int>($"SELECT PB_STATUS FROM OPS_HEADER WHERE PB_ID={order}")==110,"line haul dispatch preserves consignee order status");
        Check(await Scalar<int>($"SELECT COUNT(*) FROM OPS_STOP_REC WHERE OPS_STOP_ID={stop} AND OPS_DISPATCH_TIME IS NULL AND OPS_DELIVERED_TIME IS NULL")==1,"line haul release does not claim final delivery dispatch");
        await Exec("ROLLBACK TO SAVEPOINT before_release");
        var stale=await Read();
        await Exec("UPDATE OPS_MANIFEST_STOPS SET OSM_SEQUENCE=2 WHERE OSM_STOP_ID=@route",new{route});
        await Reject(()=>Write(stale,d2,t,true),"changed");
        await Exec("DELETE FROM OPS_MANIFEST_STOPS WHERE OSM_STOP_ID=@route",new{route});
        await Reject(async ()=>await Write(await Read(),d2,t,true),"Add orders");
    }
    finally { await tx.RollbackAsync(); }
    Check(await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM OPS_HEADER WHERE PB_ID IN ({m},{order},{other})")==0,"all synthetic business data rolled back");
}
Console.WriteLine($"PASS {checks} dispatch checks.");
sealed class TestTenant(Guid id):ITenantContext {public Guid TenantId=>id;public Guid? TenantIdOrNull=>id;public Guid? UserIdOrNull=>null;}


